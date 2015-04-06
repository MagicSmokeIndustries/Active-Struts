using System.Linq;
using ActiveStruts.Util;
using UnityEngine;

namespace ActiveStruts.Modules
{
    public class ModuleAutoStrutter : PartModule
    {
        private const byte WaitInterval = 30;
        [KSPField(isPersistant = true)] public bool Enabled = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Group")] public int Group = 0;
        private bool _connected;
        private Transform _head;
        private bool _isOrigin;
        private FixedJoint _joint;
        private Transform[] _lights;
        private Transform _mount;
        private ModuleAutoStrutter _partner;
        private GameObject _strut;
        private byte _wait;

        private Transform HeadTransform
        {
            get { return this._head != null ? this._head.transform : this.part.transform; }
        }

        private Transform MountTransform
        {
            get { return this._mount != null ? this._mount.transform : this.part.transform; }
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, unfocusedRange = 15f, externalToEVAOnly = false, guiName = "Group -")]
        public void DecreaseGroup()
        {
            if (this.Group > 0)
            {
                this.Group--;
            }
        }

        [KSPAction("DecreaseGroupAction", KSPActionGroup.None, guiName = "Group -")]
        public void DecreaseGroupAction(KSPActionParam param)
        {
            this.DecreaseGroup();
        }

        [KSPEvent(guiName = "Disable", name = "Disable", unfocusedRange = 15f, externalToEVAOnly = false)]
        public void Disable()
        {
            this.Enabled = false;
            this._unlink();
        }

        [KSPEvent(guiName = "Enable", name = "Enable", unfocusedRange = 15f, externalToEVAOnly = false)]
        public void Enable()
        {
            this.Enabled = true;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            if (this._connected)
            {
                if (!this.Enabled
                    || this._partner == null
                    || !this._partner.Enabled
                    || this._partner.vessel == null
                    || this.vessel == null
                    || this._partner.vessel != this.vessel
                    || !this._checkGroup(this._partner))
                {
                    this._unlink();
                    return;
                }
                this._orientHeadTransformToTarget();
                this._orientMountTransformToTarget();
                if (this._joint == null)
                {
                    this._createJoint();
                }
            }
            if (this._isOrigin)
            {
                this._alignStrut();
            }
            if (this._wait > 0)
            {
                this._wait--;
                return;
            }
            this._wait = WaitInterval;
            this._updateGui();
            this._updateLights();
            if (!this._connected && this.Enabled)
            {
                var nearestStrutter = this._findNearestAutoStrutterOnVessel();
                if (nearestStrutter != null)
                {
                    this._connected = true;
                    nearestStrutter._connected = true;
                    this._partner = nearestStrutter;
                    nearestStrutter._partner = this;
                    this._isOrigin = true;
                    nearestStrutter._isOrigin = false;
                    this._createJoint();
                }
            }
        }

        [KSPAction("Group +")]
        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, unfocusedRange = 15f, externalToEVAOnly = false, guiName = "Group +")]
        public void IncreaseGroup()
        {
            if (this.Group < int.MaxValue)
            {
                this.Group++;
            }
        }

        [KSPAction("IncreaseGroupAction", KSPActionGroup.None, guiName = "Group +")]
        public void IncreaseGroupAction(KSPActionParam param)
        {
            this.IncreaseGroup();
        }

        public void Start()
        {
            this._wait = WaitInterval;
            this._head = this.part.FindModelTransform("Head");
            this._mount = this.part.FindModelTransform("Mount");
            this._lights = new[]
                           {
                               this.part.FindModelTransform("Light1"), this.part.FindModelTransform("Light2"), this.part.FindModelTransform("SmoothLight1_1"), this.part.FindModelTransform("SmoothLight1_2"),
                               this.part.FindModelTransform("SmoothLight2_1"), this.part.FindModelTransform("SmoothLight2_2")
                           };
            this._connected = false;
            if (HighLogic.LoadedSceneIsFlight)
            {
                this._strut = Utilities.CreateSimpleStrut("Autostrutterstrut");
            }
        }

        [KSPAction("ToggleEnabledAction", KSPActionGroup.None, guiName = "Toggle Enabled")]
        public void ToggleEnabledAction(KSPActionParam param)
        {
            if (this.Enabled)
            {
                this.Disable();
            }
            else
            {
                this.Enable();
            }
        }

        private void _alignStrut()
        {
            if (this._partner != null && this._strut != null)
            {
                this._strut.SetActive(false);
                var dist = Vector3.Distance(Vector3.zero, this.HeadTransform.InverseTransformPoint(this._partner.HeadTransform.position))/2f;
                var trans = this._strut.transform;
                trans.position = this.HeadTransform.position;
                trans.LookAt(this._partner.HeadTransform.position);
                trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                trans.localScale = new Vector3(0.025f, dist, 0.025f);
                trans.Rotate(new Vector3(0, 0, 1), 90f);
                trans.Rotate(new Vector3(1, 0, 0), 90f);
                trans.Translate(new Vector3(0f, 1f, 0f)*dist);
                this._strut.SetActive(true);
            }
        }

        private bool _checkGroup(ModuleAutoStrutter possibleTarget)
        {
            if (Config.Instance.AutoStrutterConnectToOwnGroup)
            {
                return this.Group == possibleTarget.Group;
            }
            return this.Group != possibleTarget.Group;
        }

        private bool _checkLineOfSight(ModuleAutoStrutter target)
        {
            var rayres = Utilities.PerformRaycast(this.HeadTransform.position, target.HeadTransform.position, this.part.transform.up, this.part);
            return rayres.HitResult && rayres.HittedPart != null && rayres.HittedPart == target.part && rayres.RayAngle <= Config.Instance.MaxAngleAutostrutter;
        }

        private void _createJoint()
        {
            if (this._partner == null || this._partner.rigidbody == null)
            {
                return;
            }
            this._joint = this.part.gameObject.AddComponent<FixedJoint>();
            this._joint.breakForce = this._joint.breakTorque = Mathf.Infinity;
            this._joint.connectedBody = this._partner.rigidbody;
        }

        private ModuleAutoStrutter _findNearestAutoStrutterOnVessel()
        {
            return (from p in this.part.vessel.Parts
                    let mod = p.FindModuleImplementing<ModuleAutoStrutter>()
                    where mod != null && mod.Enabled && !mod._connected
                    where this._checkGroup(mod)
                          && mod.part.parent != null
                          && this.part.parent != null
                          && this.part.parent != mod.part.parent
                    where this._checkLineOfSight(mod)
                    orderby Vector3.Distance(mod.HeadTransform.position, this.HeadTransform.position)
                    select mod).FirstOrDefault();
        }

        private void _orientHeadTransformToTarget()
        {
            this.HeadTransform.LookAt(this._partner.HeadTransform.position);
        }

        private void _orientMountTransformToTarget()
        {
            var angle = RotationAngleHelper.GetYRotationAngleToLookAtTarget(this.MountTransform, this._partner.MountTransform);
            this.MountTransform.Rotate(new Vector3(0f, 1f, 0f), angle + 90f);
        }

        private void _unlink()
        {
            if (this._joint != null)
            {
                Destroy(this._joint);
            }
            this._strut.transform.localScale = Vector3.zero;
            this._strut.SetActive(false);
            if (this._partner != null && this._isOrigin)
            {
                this._partner._unlink();
            }
            this._partner = null;
            this._connected = this._isOrigin = false;
        }

        private void _updateGui()
        {
            var disableEvent = this.Events["Disable"];
            var enableEvent = this.Events["Enable"];
            if (this.Enabled)
            {
                disableEvent.active = disableEvent.guiActive = disableEvent.guiActiveEditor = disableEvent.guiActiveUnfocused = true;
                enableEvent.active = enableEvent.guiActive = enableEvent.guiActiveEditor = enableEvent.guiActiveUnfocused = false;
            }
            else
            {
                disableEvent.active = disableEvent.guiActive = disableEvent.guiActiveEditor = disableEvent.guiActiveUnfocused = false;
                enableEvent.active = enableEvent.guiActive = enableEvent.guiActiveEditor = enableEvent.guiActiveUnfocused = true;
            }
        }

        private void _updateLights()
        {
            var col = Color.white;
            if (this._connected)
            {
                col = Utilities._setColorForEmissive(Color.green);
            }
            else if (!this._connected && this.Enabled)
            {
                col = Utilities._setColorForEmissive(new Color(1f, 0.6f, 0f));
            }
            else if (!this.Enabled)
            {
                col = Utilities._setColorForEmissive(Color.red);
            }
            foreach (var l in this._lights)
            {
                l.renderer.material.color = col;
                l.renderer.material.SetColor("_Emissive", col);
                l.renderer.material.SetColor("_MainTex", col);
                l.renderer.material.SetColor("_EmissiveColor", col);
            }
        }
    }
}