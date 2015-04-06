using System;
using System.Linq;
using ActiveStruts.Addons;
using ActiveStruts.Util;
using UnityEngine;

namespace ActiveStruts.Modules
{
    public class ModuleActiveStrutFreeAttachTarget : PartModule, IDResetable
    {
        private const float InFlightScale = 0.01f; //default = 0.01f, debug = 0.1f
        [KSPField(isPersistant = true)] public bool CreatedInEditor = false;
        [KSPField(isPersistant = true)] public string Id = Guid.Empty.ToString();
        [KSPField(isPersistant = true)] public bool IdResetDone = false;
        [KSPField(isPersistant = false)] public bool IsKerbalHook = false;
        [KSPField(isPersistant = false)] public float TetherOffset = 0f;

        public Guid ID
        {
            get
            {
                var guid = new Guid(this.Id);
                if (guid != Guid.Empty)
                {
                    return guid;
                }
                guid = Guid.NewGuid();
                this.Id = guid.ToString();
                return guid;
            }
            set { this.Id = value.ToString(); }
        }

        public Vector3 OffsetPosition
        {
            get { return this.PartOrigin.position + (this.PartOrigin.up*this.TetherOffset); }
        }

        public Transform PartOrigin
        {
            get { return this.part.transform; }
        }

        public Rigidbody PartRigidbody
        {
            get { return this.part.rigidbody; }
        }

        public ModuleActiveStrut Targeter { get; set; }

        public void ResetId()
        {
            var oldId = this.Id;
            this.Id = Guid.NewGuid().ToString();
            foreach (var moduleActiveStrut in Utilities.GetAllActiveStruts().Where(m => m.FreeAttachTargetId != null))
            {
                if (moduleActiveStrut.FreeAttachTargetId == oldId)
                {
                    moduleActiveStrut.FreeAttachTargetId = this.Id;
                }
            }
            this.IdResetDone = true;
        }

        internal bool CreateJointToParent(Part parent)
        {
            this.part.attachMode = AttachModes.SRF_ATTACH;
            this.part.srfAttachNode.attachedPart = parent;
            this.part.srfAttachNode.breakingForce = Mathf.Infinity;
            this.part.srfAttachNode.breakingTorque = Mathf.Infinity;
            this.part.srfAttachNode.ResourceXFeed = true;
            this.part.srfAttachNode.position = this.part.srfAttachNode.originalPosition = this.part.transform.position;

            this.part.Couple(parent);

            this.part.SendMessage("OnAttach", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[AS] spawned part attached to its parent.");
            return true;
        }

        internal void Die()
        {
            Debug.Log("[AS] targetpart tries to die");
            if (HighLogic.LoadedSceneIsFlight)
            {
                this.part.decouple();
                this.part.isPersistent = false;
                this.part.transform.localScale = Vector3.zero;
                this.part.deactivate();
                Destroy(this.part.gameObject);
                Destroy(this.part);
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                this.part.transform.localScale = Vector3.zero;
            }
        }

        public override void OnStart(StartState state)
        {
            if (this.Id == Guid.Empty.ToString())
            {
                this.Id = Guid.NewGuid().ToString();
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                this.part.OnEditorAttach += this._processEditorAttach;
                this.CreatedInEditor = true;
            }
            if (HighLogic.LoadedSceneIsFlight && !this.IdResetDone)
            {
                ActiveStrutsAddon.Enqueue(this);
            }
            if (this.part.collider != null && this.part.FindModuleImplementing<ModuleKerbalHookAnchor>() == null)
            {
                Destroy(this.part.collider);
            }
            if (!this.IsKerbalHook && HighLogic.LoadedSceneIsFlight)
            {
                this.part.transform.localScale = new Vector3(InFlightScale, InFlightScale, InFlightScale);
            }
            this.part.force_activate();
        }

        private void _processEditorAttach()
        {
            var allTargets = Utilities.GetAllFreeAttachTargets();
            if (allTargets == null)
            {
                return;
            }
            if (allTargets.Any(t => t.ID == this.ID && t != this))
            {
                this.ID = Guid.NewGuid();
                if (this.Targeter != null)
                {
                    this.Targeter.FreeAttachTargetId = this.ID.ToString();
                }
            }
        }
    }
}