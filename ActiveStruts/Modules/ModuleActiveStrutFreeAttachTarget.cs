using System;
using System.Linq;
using ActiveStruts.Addons;
using ActiveStruts.Util;
using UnityEngine;

namespace ActiveStruts.Modules
{
    public class ModuleActiveStrutFreeAttachTarget : PartModule, IDResetable
    {
        private const float IN_FLIGHT_SCALE = 0.01f; //default = 0.01f, debug = 0.1f
        [KSPField(isPersistant = true)] public bool CreatedInEditor = false;
        [KSPField(isPersistant = true)] public string Id = Guid.Empty.ToString();
        [KSPField(isPersistant = true)] public bool IdResetDone = false;
        [KSPField(isPersistant = false)] public bool IsKerbalHook = false;
        [KSPField(isPersistant = false)] public float TetherOffset = 0f;

        public Guid ID
        {
            get
            {
                var guid = new Guid(Id);
                if (guid != Guid.Empty)
                {
                    return guid;
                }
                guid = Guid.NewGuid();
                Id = guid.ToString();
                return guid;
            }
            set { Id = value.ToString(); }
        }

        public Vector3 OffsetPosition
        {
            get { return PartOrigin.position + (PartOrigin.up*TetherOffset); }
        }

        public Transform PartOrigin
        {
            get { return part.transform; }
        }

        public Rigidbody PartRigidbody
        {
            get { return part.GetComponent<Rigidbody>(); }
        }

        public ModuleActiveStrut Targeter { get; set; }

        public void ResetId()
        {
            var oldId = Id;
            Id = Guid.NewGuid().ToString();
            foreach (var moduleActiveStrut in Utilities.GetAllActiveStruts().Where(m => m.FreeAttachTargetId != null))
            {
                if (moduleActiveStrut.FreeAttachTargetId == oldId)
                {
                    moduleActiveStrut.FreeAttachTargetId = Id;
                }
            }
            IdResetDone = true;
        }

        internal bool CreateJointToParent(Part parent)
        {
            part.attachMode = AttachModes.SRF_ATTACH;
            part.srfAttachNode.attachedPart = parent;
            part.srfAttachNode.breakingForce = Mathf.Infinity;
            part.srfAttachNode.breakingTorque = Mathf.Infinity;
            part.srfAttachNode.ResourceXFeed = true;
            part.srfAttachNode.position = part.srfAttachNode.originalPosition = part.transform.position;

            part.Couple(parent);

            part.SendMessage("OnAttach", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[IRAS] spawned part attached to its parent.");
            return true;
        }

        internal void Die()
        {
            Debug.Log("[IRAS] targetpart tries to die");
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.decouple();
                part.isPersistent = false;
                part.transform.localScale = Vector3.zero;
                part.deactivate();
                Destroy(part.gameObject);
                Destroy(part);
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                part.transform.localScale = Vector3.zero;
            }
        }

        public override void OnStart(StartState state)
        {
            if (Id == Guid.Empty.ToString())
            {
                Id = Guid.NewGuid().ToString();
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                part.OnEditorAttach += _processEditorAttach;
                CreatedInEditor = true;
            }
            if (HighLogic.LoadedSceneIsFlight && !IdResetDone)
            {
                ActiveStrutsAddon.Enqueue(this);
            }
            if (part.collider != null && part.FindModuleImplementing<ModuleKerbalHookAnchor>() == null)
            {
                Destroy(part.collider);
            }
            if (!IsKerbalHook && HighLogic.LoadedSceneIsFlight)
            {
                part.transform.localScale = new Vector3(IN_FLIGHT_SCALE, IN_FLIGHT_SCALE, IN_FLIGHT_SCALE);
            }
            part.force_activate();
        }

        private void _processEditorAttach()
        {
            var allTargets = Utilities.GetAllFreeAttachTargets();
            if (allTargets == null)
            {
                return;
            }
            if (allTargets.Any(t => t.ID == ID && t != this))
            {
                ID = Guid.NewGuid();
                if (Targeter != null)
                {
                    Targeter.FreeAttachTargetId = ID.ToString();
                }
            }
        }
    }
}