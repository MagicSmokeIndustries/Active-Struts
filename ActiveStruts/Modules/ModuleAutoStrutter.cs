using System.Linq;
using ActiveStruts.Util;
using UnityEngine;

namespace ActiveStruts.Modules
{
    public class ModuleAutoStrutter : PartModule
    {
        private const byte WAIT_INTERVAL = 30;
        [KSPField(isPersistant = true)] public bool Enabled = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Group")] public int Group =
            0;

        private bool connected;
        private Transform head;
        private bool isOrigin;
        private FixedJoint joint;
        private Transform[] lights;
        private Transform mount;
        private ModuleAutoStrutter partner;
        private GameObject strut;
        private byte wait;

        private Transform HeadTransform
        {
            get { return head != null ? head.transform : part.transform; }
        }

        private Transform MountTransform
        {
            get { return mount != null ? mount.transform : part.transform; }
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, unfocusedRange = 15f,
            externalToEVAOnly = false, guiName = "Group -")]
        public void DecreaseGroup()
        {
            if (Group > 0)
            {
                Group--;
            }
        }

        [KSPAction("DecreaseGroupAction", KSPActionGroup.None, guiName = "Group -")]
        public void DecreaseGroupAction(KSPActionParam param)
        {
            DecreaseGroup();
        }

        [KSPEvent(guiName = "Disable", name = "Disable", unfocusedRange = 15f, externalToEVAOnly = false)]
        public void Disable()
        {
            Enabled = false;
            Unlink();
        }

        [KSPEvent(guiName = "Enable", name = "Enable", unfocusedRange = 15f, externalToEVAOnly = false)]
        public void Enable()
        {
            Enabled = true;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            if (connected)
            {
                if (!Enabled
                    || partner == null
                    || !partner.Enabled
                    || partner.vessel == null
                    || vessel == null
                    || partner.vessel != vessel
                    || !CheckGroup(partner))
                {
                    Unlink();
                    return;
                }
                OrientHeadTransformToTarget();
                OrientMountTransformToTarget();
                if (joint == null)
                {
                    CreateJoint();
                }
            }
            if (isOrigin)
            {
                AlignStrut();
            }
            if (wait > 0)
            {
                wait--;
                return;
            }
            wait = WAIT_INTERVAL;
            UpdateGui();
            UpdateLights();
            if (!connected && Enabled)
            {
                var nearestStrutter = FindNearestAutoStrutterOnVessel();
                if (nearestStrutter != null)
                {
                    connected = true;
                    nearestStrutter.connected = true;
                    partner = nearestStrutter;
                    nearestStrutter.partner = this;
                    isOrigin = true;
                    nearestStrutter.isOrigin = false;
                    CreateJoint();
                }
            }
        }

        [KSPAction("Group +")]
        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, unfocusedRange = 15f,
            externalToEVAOnly = false, guiName = "Group +")]
        public void IncreaseGroup()
        {
            if (Group < int.MaxValue)
            {
                Group++;
            }
        }

        [KSPAction("IncreaseGroupAction", KSPActionGroup.None, guiName = "Group +")]
        public void IncreaseGroupAction(KSPActionParam param)
        {
            IncreaseGroup();
        }

        public void Start()
        {
            wait = WAIT_INTERVAL;
            head = part.FindModelTransform("Head");
            mount = part.FindModelTransform("Mount");
            lights = new[]
            {
                part.FindModelTransform("Light1"), part.FindModelTransform("Light2"),
                part.FindModelTransform("SmoothLight1_1"), part.FindModelTransform("SmoothLight1_2"),
                part.FindModelTransform("SmoothLight2_1"), part.FindModelTransform("SmoothLight2_2")
            };
            connected = false;
            if (HighLogic.LoadedSceneIsFlight)
            {
                strut = Utilities.CreateSimpleStrut("Autostrutterstrut");
            }
        }

        [KSPAction("ToggleEnabledAction", KSPActionGroup.None, guiName = "Toggle Enabled")]
        public void ToggleEnabledAction(KSPActionParam param)
        {
            if (Enabled)
            {
                Disable();
            }
            else
            {
                Enable();
            }
        }

        private void AlignStrut()
        {
            if (partner != null && strut != null)
            {
                strut.SetActive(false);
                var dist =
                    Vector3.Distance(Vector3.zero, HeadTransform.InverseTransformPoint(partner.HeadTransform.position))/
                    2f;
                var trans = strut.transform;
                trans.position = HeadTransform.position;
                trans.LookAt(partner.HeadTransform.position);
                trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                trans.localScale = new Vector3(0.025f, dist, 0.025f);
                trans.Rotate(new Vector3(0, 0, 1), 90f);
                trans.Rotate(new Vector3(1, 0, 0), 90f);
                trans.Translate(new Vector3(0f, 1f, 0f)*dist);
                strut.SetActive(true);
            }
        }

        private bool CheckGroup(ModuleAutoStrutter possibleTarget)
        {
            if (Config.Instance.AutoStrutterConnectToOwnGroup)
            {
                return Group == possibleTarget.Group;
            }
            return Group != possibleTarget.Group;
        }

        private bool _checkLineOfSight(ModuleAutoStrutter target)
        {
            var rayres = Utilities.PerformRaycast(HeadTransform.position, target.HeadTransform.position,
                part.transform.up, part);
            return rayres.HitResult && rayres.HittedPart != null && rayres.HittedPart == target.part &&
                   rayres.RayAngle <= Config.Instance.MaxAngleAutostrutter;
        }

        private void CreateJoint()
        {
            if (partner == null || partner.GetComponent<Rigidbody>() == null)
            {
                return;
            }
            joint = part.gameObject.AddComponent<FixedJoint>();
            joint.breakForce = joint.breakTorque = Mathf.Infinity;
            joint.connectedBody = partner.GetComponent<Rigidbody>();
        }

        private ModuleAutoStrutter FindNearestAutoStrutterOnVessel()
        {
            return (from p in part.vessel.Parts
                let mod = p.FindModuleImplementing<ModuleAutoStrutter>()
                where mod != null && mod.Enabled && !mod.connected
                where CheckGroup(mod)
                      && mod.part.parent != null
                      && part.parent != null
                      && part.parent != mod.part.parent
                where _checkLineOfSight(mod)
                orderby Vector3.Distance(mod.HeadTransform.position, HeadTransform.position)
                select mod).FirstOrDefault();
        }

        private void OrientHeadTransformToTarget()
        {
            HeadTransform.LookAt(partner.HeadTransform.position);
        }

        private void OrientMountTransformToTarget()
        {
            var angle = RotationAngleHelper.GetYRotationAngleToLookAtTarget(MountTransform, partner.MountTransform);
            MountTransform.Rotate(new Vector3(0f, 1f, 0f), angle + 90f);
        }

        private void Unlink()
        {
            if (joint != null)
            {
                Destroy(joint);
            }
            strut.transform.localScale = Vector3.zero;
            strut.SetActive(false);
            if (partner != null && isOrigin)
            {
                partner.Unlink();
            }
            partner = null;
            connected = isOrigin = false;
        }

        private void UpdateGui()
        {
            var disableEvent = Events["Disable"];
            var enableEvent = Events["Enable"];
            if (Enabled)
            {
                disableEvent.active =
                    disableEvent.guiActive = disableEvent.guiActiveEditor = disableEvent.guiActiveUnfocused = true;
                enableEvent.active =
                    enableEvent.guiActive = enableEvent.guiActiveEditor = enableEvent.guiActiveUnfocused = false;
            }
            else
            {
                disableEvent.active =
                    disableEvent.guiActive = disableEvent.guiActiveEditor = disableEvent.guiActiveUnfocused = false;
                enableEvent.active =
                    enableEvent.guiActive = enableEvent.guiActiveEditor = enableEvent.guiActiveUnfocused = true;
            }
        }

        private void UpdateLights()
        {
            var col = Color.white;
            if (connected)
            {
                col = Utilities.SetColorForEmissive(Color.green);
            }
            else if (!connected && Enabled)
            {
                col = Utilities.SetColorForEmissive(new Color(1f, 0.6f, 0f));
            }
            else if (!Enabled)
            {
                col = Utilities.SetColorForEmissive(Color.red);
            }
            foreach (var l in lights)
            {
                var r = l.GetComponent<Renderer> ();
                if (!r)
                    continue;
                
                r.material.color = col;
                r.material.SetColor("_Emissive", col);
                r.material.SetColor("_MainTex", col);
                r.material.SetColor("_EmissiveColor", col);
            }
        }
    }
}