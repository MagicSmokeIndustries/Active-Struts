using System;
using System.Collections;
using ActiveStruts.Addons;
using ActiveStruts.Util;
using UnityEngine;
using OSD = ActiveStruts.Util.OSD;
using Utilities = ActiveStruts.Util.Utilities;

namespace ActiveStruts.Modules
{
    public class ModuleKerbalHook : PartModule
    {
        private const string THROW_HOOK_LABEL = "ThrowHook";
        private const string RELEASE_HOOK_LABEL = "ReleaseHook";
        private const string PULL_CLOSER_LABEL = "PullCloser";
        private const string INCREASE_CABLE_LENGTH_LABEL = "IncreaseCableLength";
        private const float WINCH_STEP_WIDTH = 0.5f;
        private const float MIN_TETHER_LENGTH = 0.5f;
        private readonly object jointLock = new object();
        [KSPField(guiName = "Tether Length", guiActive = true, guiFormat = "F1")] public float MaxDistance;
        internal ModuleActiveStrutFreeAttachTarget Target;
        private ModuleKerbalHookAnchor hookAnchor;
        private SpringJoint joint;
        private GameObject localAnchor;
        private FixedJoint localAnchorJoint;
        private GameObject strut;
        private bool strutActive;

        public void Abort()
        {
            lock (jointLock)
            {
                if (joint != null)
                {
                    DestroyImmediate(joint);
                }
            }
            if (hookAnchor != null)
            {
                ReleaseHookAnchor();
                return;
            }
            if (Target != null)
            {
                Target.Die();
            }
            Target = null;
            if (localAnchor != null)
            {
                if (localAnchorJoint != null)
                {
                    DestroyImmediate(localAnchorJoint);
                }
                Destroy(localAnchor);
            }
        }

        internal void Die()
        {
            Abort();
        }

        [KSPEvent(name = INCREASE_CABLE_LENGTH_LABEL, active = false, guiActive = true, guiName = "Increase tether length",
            guiActiveEditor = false, guiActiveUnfocused = false)]
        public void IncreaseCableLength()
        {
            if (MaxDistance + WINCH_STEP_WIDTH < Config.Instance.MaxDistance)
            {
                MaxDistance += WINCH_STEP_WIDTH;
            }
            else
            {
                OSD.PostMessageLowerRightCorner("max tether length reached");
            }
        }

        public override void OnUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || (Target == null && hookAnchor == null))
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    _updateGui();
                }
                if (strutActive && strut != null)
                {
                    strut.SetActive(false);
                }
                return;
            }
            if (Target != null && joint == null && localAnchor != null)
            {
                localAnchor.transform.position = Target.part.transform.position;
            }
            if (joint != null)
            {
                _realignStrut();
                joint.maxDistance = MaxDistance;
            }
            _updateGui();
        }

        public override void OnStart(StartState state)
        {
            part.force_activate();
        }

        public void PlaceHook(Part newSpawnedPart)
        {
            ActiveStrutsAddon.Mode = AddonMode.None;
            var module = _getModuleFromPart(newSpawnedPart);
            if (module == null)
            {
                //submit for removal
                ActiveStrutsAddon.NewSpawnedPart = newSpawnedPart;
                return;
            }
            MaxDistance = -1f;
            Target = module;
            _createLocalAnchor();
            StartCoroutine(WaitAndCreateJoint());
        }

        private IEnumerator PreparePartForFreeAttach()
        {
            var newPart = PartFactory.SpawnPartInFlight("TetherHook", part, new Vector3(2, 2, 2),
                part.transform.rotation);
            OSD.PostMessageLowerRightCorner("waiting for Unity to catch up...", 1.5f);
            while (!newPart.GetComponent<Rigidbody>())
            {
                Debug.Log("[IRAS] rigidbody not ready - waiting");
                try
                {
                    DestroyImmediate(newPart.collider);
                }
                catch (Exception)
                {
                    //sanity reason
                }
                try
                {
                    newPart.transform.position = part.transform.position;
                }
                catch (NullReferenceException)
                {
                    //sanity reason
                }
                try
                {
                    newPart.mass = 0.000001f;
                    newPart.maximum_drag = 0f;
                    newPart.minimum_drag = 0f;
                }
                catch (NullReferenceException)
                {
                    //sanity reason
                }
                yield return new WaitForFixedUpdate();
            }
            newPart.mass = 0.000001f;
            newPart.maximum_drag = 0f;
            newPart.minimum_drag = 0f;
            ActiveStrutsAddon.NewSpawnedPart = newPart;
            ActiveStrutsAddon.CurrentKerbalTargeter = this;
            ActiveStrutsAddon.Mode = AddonMode.AttachKerbalHook;
        }

        [KSPEvent(name = PULL_CLOSER_LABEL, active = false, guiActive = true, guiName = "Pull closer",
            guiActiveEditor = false, guiActiveUnfocused = false)]
        public void PullCloser()
        {
            if (MaxDistance - WINCH_STEP_WIDTH > MIN_TETHER_LENGTH)
            {
                MaxDistance -= WINCH_STEP_WIDTH;
            }
            else
            {
                OSD.PostMessageLowerRightCorner("min tether length reached");
            }
        }

        [KSPEvent(name = RELEASE_HOOK_LABEL, active = false, guiActive = true, guiName = "Release EVA Hook",
            guiActiveEditor = false, guiActiveUnfocused = false)]
        public void ReleaseHook()
        {
            Abort();
        }

        [KSPEvent(name = THROW_HOOK_LABEL, active = false, guiActive = true, guiName = "Throw Tether Hook",
            guiActiveEditor = false, guiActiveUnfocused = false)]
        public void ThrowHook()
        {
            if (Config.Instance.ShowHelpTexts)
            {
                OSD.PostMessage(Config.Instance.FreeAttachHelpText, 5);
            }
            StartCoroutine(PreparePartForFreeAttach());
        }

        private IEnumerator WaitAndCreateJoint()
        {
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            var conf = Config.Instance;
            localAnchor.transform.position = Target.part.transform.position;
            MaxDistance = Vector3.Distance(part.transform.position, Target.transform.position);
            joint = localAnchor.AddComponent<SpringJoint>();
            joint.spring = conf.KerbalTetherSpringForce;
            joint.damper = conf.KerbalTetherDamper;
            joint.anchor = Target.part.transform.position;
            joint.connectedBody = Target.part.parent.GetComponent<Rigidbody>();
            joint.maxDistance = MaxDistance + 0.25f;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            localAnchor.transform.position = part.transform.position;
            localAnchorJoint = localAnchor.AddComponent<FixedJoint>();
            localAnchorJoint.connectedBody = part.GetComponent<Rigidbody>();
            localAnchorJoint.breakForce = localAnchorJoint.breakTorque = Mathf.Infinity;
        }

        private void _createJointNow()
        {
            if (hookAnchor == null)
            {
                ReleaseHook();
                return;
            }
            var conf = Config.Instance;
            var tPos = hookAnchor.part.transform.position;
            MaxDistance = Vector3.Distance(part.transform.position, tPos);
            joint = part.gameObject.AddComponent<SpringJoint>();
            joint.spring = conf.KerbalTetherSpringForce;
            joint.damper = conf.KerbalTetherDamper;
            joint.anchor = tPos;
            joint.connectedBody = hookAnchor.part.GetComponent<Rigidbody>();
            joint.maxDistance = MaxDistance + 0.25f;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }

        private void _createLocalAnchor()
        {
            localAnchor = Utilities.CreateLocalAnchor("KerbalHookLocalAnchor", true);
        }

        private void _createStrut()
        {
            //Color = 255, 209, 25
            strut = Utilities.CreateFlexStrut("KerbalHookStrut", true, new Color(1f, 0.81961f, 0.09804f));
        }

        private static ModuleActiveStrutFreeAttachTarget _getModuleFromPart(Part spPart)
        {
            if (!spPart.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget))
            {
                return null;
            }
            var module =
                spPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget;
            return module;
        }

        private void _realignStrut()
        {
            if (strut != null)
            {
                var target = Target == null
                    ? hookAnchor != null
                        ? hookAnchor.part.FindModuleImplementing<ModuleActiveStrutFreeAttachTarget>()
                        : null
                    : null;
                if (target != null)
                {
                    strut.SetActive(false);
                    var trans = strut.transform;
                    trans.position = part.transform.position;
                    trans.LookAt(target.OffsetPosition);
                    trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                    var dist = (Vector3.Distance(Vector3.zero, trans.InverseTransformPoint(target.OffsetPosition))/2.0f);
                    trans.localScale = new Vector3(0.0125f, dist, 0.0125f);
                    trans.Rotate(new Vector3(0, 0, 1), 90f);
                    trans.Rotate(new Vector3(1, 0, 0), 90f);
                    trans.Translate(new Vector3(0f, dist, 0f));
                    strut.SetActive(true);
                    strutActive = true;
                }
                else
                {
                    strut.SetActive(false);
                    strutActive = false;
                }
            }
            else
            {
                _createStrut();
            }
        }

        private void _updateGui()
        {
            var hookEvent = Events[THROW_HOOK_LABEL];
            var releaseEvent = Events[RELEASE_HOOK_LABEL];
            var pullEvent = Events[PULL_CLOSER_LABEL];
            var increaseLengthEvent = Events[INCREASE_CABLE_LENGTH_LABEL];
            var lengthField = Fields["MaxDistance"];
            if (Target == null && hookAnchor == null)
            {
                if (Config.Instance.EnableFreeAttachKerbalTether)
                {
                    hookEvent.active = hookEvent.guiActive = true;
                }
                releaseEvent.active = releaseEvent.guiActive = false;
                pullEvent.active = pullEvent.guiActive = false;
                increaseLengthEvent.active = increaseLengthEvent.guiActive = false;
                lengthField.guiActive = false;
            }
            else
            {
                hookEvent.active = hookEvent.guiActive = false;
                releaseEvent.active = releaseEvent.guiActive = true;
                pullEvent.active = pullEvent.guiActive = true;
                increaseLengthEvent.active = increaseLengthEvent.guiActive = true;
                lengthField.guiActive = true;
            }
        }

        internal void SetHookAnchor(ModuleKerbalHookAnchor anchor)
        {
            hookAnchor = anchor;
            if (hookAnchor != null)
            {
                hookAnchor.IsConnected = true;
                hookAnchor.KerbalHook = this;
                _createJointNow();
            }
        }

        internal void ReleaseHookAnchor()
        {
            if (hookAnchor != null)
            {
                hookAnchor.IsConnected = false;
                hookAnchor.KerbalHook = null;
            }
            hookAnchor = null;
            Abort();
        }
    }
}