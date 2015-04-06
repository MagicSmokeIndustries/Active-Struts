using System;
using System.Collections;
using System.Runtime.CompilerServices;
using ActiveStruts.Addons;
using ActiveStruts.Util;
using CIT_Util;
using UnityEngine;
using OSD = ActiveStruts.Util.OSD;
using Utilities = ActiveStruts.Util.Utilities;

namespace ActiveStruts.Modules
{
    public class ModuleKerbalHook : PartModule
    {
        private const string ThrowHookLabel = "ThrowHook";
        private const string ReleaseHookLabel = "ReleaseHook";
        private const string PullCloserLabel = "PullCloser";
        private const string IncreaseCableLengthLabel = "IncreaseCableLength";
        private const float WinchStepWidth = 0.5f;
        private const float MinTetherLength = 0.5f;
        private readonly object _jointLock = new object();
        [KSPField(guiName = "Tether Length", guiActive = true, guiFormat = "F1")] public float MaxDistance;
        internal ModuleActiveStrutFreeAttachTarget Target;
        private SpringJoint _joint;
        private GameObject _localAnchor;
        private FixedJoint _localAnchorJoint;
        private GameObject _strut;
        private bool _strutActive;

        public void Abort()
        {
            lock (this._jointLock)
            {
                if (this._joint != null)
                {
                    DestroyImmediate(this._joint);
                }
            }
            if (this._hookAnchor != null)
            {
                this.ReleaseHookAnchor();
                return;
            }
            if (this.Target != null)
            {
                this.Target.Die();
            }
            this.Target = null;
            if (this._localAnchor != null)
            {
                if (this._localAnchorJoint != null)
                {
                    DestroyImmediate(this._localAnchorJoint);
                }
                Destroy(this._localAnchor);
            }
        }

        internal void Die()
        {
            this.Abort();
        }

        [KSPEvent(name = IncreaseCableLengthLabel, active = false, guiActive = true, guiName = "Increase tether length", guiActiveEditor = false, guiActiveUnfocused = false)]
        public void IncreaseCableLength()
        {
            if (this.MaxDistance + WinchStepWidth < Config.Instance.MaxDistance)
            {
                this.MaxDistance += WinchStepWidth;
            }
            else
            {
                OSD.PostMessageLowerRightCorner("max tether length reached");
            }
        }

        public override void OnUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || (this.Target == null && this._hookAnchor == null))
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    this._updateGui();
                }
                if (this._strutActive && this._strut != null)
                {
                    this._strut.SetActive(false);
                }
                return;
            }
            if (this.Target != null && this._joint == null && this._localAnchor != null)
            {
                this._localAnchor.transform.position = this.Target.part.transform.position;
            }
            if (this._joint != null)
            {
                this._realignStrut();
                this._joint.maxDistance = this.MaxDistance;
            }
            this._updateGui();
        }

        public override void OnStart(StartState state)
        {
            this.part.force_activate();
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
            this.MaxDistance = -1f;
            this.Target = module;
            this._createLocalAnchor();
            this.StartCoroutine(this.WaitAndCreateJoint());
        }

        private IEnumerator PreparePartForFreeAttach()
        {
            var newPart = PartFactory.SpawnPartInFlight("TetherHook", this.part, new Vector3(2, 2, 2), this.part.transform.rotation);
            OSD.PostMessageLowerRightCorner("waiting for Unity to catch up...", 1.5f);
            while (!newPart.rigidbody)
            {
                Debug.Log("[AS] rigidbody not ready - waiting");
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
                    newPart.transform.position = this.part.transform.position;
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

        [KSPEvent(name = PullCloserLabel, active = false, guiActive = true, guiName = "Pull closer", guiActiveEditor = false, guiActiveUnfocused = false)]
        public void PullCloser()
        {
            if (this.MaxDistance - WinchStepWidth > MinTetherLength)
            {
                this.MaxDistance -= WinchStepWidth;
            }
            else
            {
                OSD.PostMessageLowerRightCorner("min tether length reached");
            }
        }

        [KSPEvent(name = ReleaseHookLabel, active = false, guiActive = true, guiName = "Release EVA Hook", guiActiveEditor = false, guiActiveUnfocused = false)]
        public void ReleaseHook()
        {
            this.Abort();
        }

        [KSPEvent(name = ThrowHookLabel, active = false, guiActive = true, guiName = "Throw Tether Hook", guiActiveEditor = false, guiActiveUnfocused = false)]
        public void ThrowHook()
        {
            if (Config.Instance.ShowHelpTexts)
            {
                OSD.PostMessage(Config.Instance.FreeAttachHelpText, 5);
            }
            this.StartCoroutine(this.PreparePartForFreeAttach());
        }

        private IEnumerator WaitAndCreateJoint()
        {
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            var conf = Config.Instance;
            this._localAnchor.transform.position = this.Target.part.transform.position;
            this.MaxDistance = Vector3.Distance(this.part.transform.position, this.Target.transform.position);
            this._joint = this._localAnchor.AddComponent<SpringJoint>();
            this._joint.spring = conf.KerbalTetherSpringForce;
            this._joint.damper = conf.KerbalTetherDamper;
            this._joint.anchor = this.Target.part.transform.position;
            this._joint.connectedBody = this.Target.part.parent.rigidbody;
            this._joint.maxDistance = this.MaxDistance + 0.25f;
            this._joint.breakForce = Mathf.Infinity;
            this._joint.breakTorque = Mathf.Infinity;
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            this._localAnchor.transform.position = this.part.transform.position;
            this._localAnchorJoint = this._localAnchor.AddComponent<FixedJoint>();
            this._localAnchorJoint.connectedBody = this.part.rigidbody;
            this._localAnchorJoint.breakForce = this._localAnchorJoint.breakTorque = Mathf.Infinity;
        }

        private void _createJointNow()
        {
            if (this._hookAnchor == null)
            {
                this.ReleaseHook();
                return;
            }
            var conf = Config.Instance;
            var tPos = this._hookAnchor.part.transform.position;
            this.MaxDistance = Vector3.Distance(this.part.transform.position, tPos);
            this._joint = this.part.gameObject.AddComponent<SpringJoint>();
            this._joint.spring = conf.KerbalTetherSpringForce;
            this._joint.damper = conf.KerbalTetherDamper;
            this._joint.anchor = tPos;
            this._joint.connectedBody = this._hookAnchor.part.rigidbody;
            this._joint.maxDistance = this.MaxDistance + 0.25f;
            this._joint.breakForce = Mathf.Infinity;
            this._joint.breakTorque = Mathf.Infinity;
        }

        private void _createLocalAnchor()
        {
            this._localAnchor = Utilities.CreateLocalAnchor("KerbalHookLocalAnchor", true);
        }

        private void _createStrut()
        {
            //Color = 255, 209, 25
            this._strut = Utilities.CreateFlexStrut("KerbalHookStrut", true, new Color(1f, 0.81961f, 0.09804f));
        }

        private static ModuleActiveStrutFreeAttachTarget _getModuleFromPart(Part spPart)
        {
            if (!spPart.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget))
            {
                return null;
            }
            var module = spPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget;
            return module;
        }

        private void _realignStrut()
        {
            if (this._strut != null)
            {
                var target = this.Target == null
                                 ? this._hookAnchor != null
                                       ? this._hookAnchor.part.FindModuleImplementing<ModuleActiveStrutFreeAttachTarget>()
                                       : null
                                 : null;
                if (target != null)
                {
                    this._strut.SetActive(false);
                    var trans = this._strut.transform;
                    trans.position = this.part.transform.position;
                    trans.LookAt(target.OffsetPosition);
                    trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                    var dist = (Vector3.Distance(Vector3.zero, trans.InverseTransformPoint(target.OffsetPosition))/2.0f);
                    trans.localScale = new Vector3(0.0125f, dist, 0.0125f);
                    trans.Rotate(new Vector3(0, 0, 1), 90f);
                    trans.Rotate(new Vector3(1, 0, 0), 90f);
                    trans.Translate(new Vector3(0f, dist, 0f));
                    this._strut.SetActive(true);
                    this._strutActive = true;
                }
                else
                {
                    this._strut.SetActive(false);
                    this._strutActive = false;
                }
            }
            else
            {
                this._createStrut();
            }
        }

        private void _updateGui()
        {
            var hookEvent = this.Events[ThrowHookLabel];
            var releaseEvent = this.Events[ReleaseHookLabel];
            var pullEvent = this.Events[PullCloserLabel];
            var increaseLengthEvent = this.Events[IncreaseCableLengthLabel];
            var lengthField = this.Fields["MaxDistance"];
            if (this.Target == null && this._hookAnchor == null)
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

        private ModuleKerbalHookAnchor _hookAnchor;

        internal void SetHookAnchor(ModuleKerbalHookAnchor anchor)
        {
            this._hookAnchor = anchor;
            if (this._hookAnchor != null)
            {
                this._hookAnchor.IsConnected = true;
                this._hookAnchor.KerbalHook = this;
                this._createJointNow();
            }
        }

        internal void ReleaseHookAnchor()
        {
            if (this._hookAnchor != null)
            {
                this._hookAnchor.IsConnected = false;
                this._hookAnchor.KerbalHook = null;
            }
            this._hookAnchor = null;
            this.Abort();
        }
    }
}