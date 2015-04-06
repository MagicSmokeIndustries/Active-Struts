using System;
using System.Collections.Generic;
using System.Linq;

namespace CIT_Util.Misc
{
    public class ModuleResourceSeparator : PartModule
    {
        private const byte LockInterval = 5;
        private byte _lockCounter;

        public void Start()
        {
            this._lockCounter = LockInterval;
        }

        public void FixedUpdate()
        {
            if (this._lockCounter > 0)
            {
                this._lockCounter--;
                return;
            }
            this._lockCounter = LockInterval;
            if (this.part == null || this.part.vessel == null)
            {
                return;
            }
            this._processLocking();
        }

        private List<Part> _getAllChildren()
        {
            var children = new List<Part>();
            this.part.RecursePartList(children);
            children.Remove(this.part);
            return children;
        }

        private void _processLocking()
        {
            var children = this._getAllChildren();
            var vid = this.part.vessel.id;
            foreach (var child in children)
            {
                var module = child.FindModuleImplementing<ModuleResourceLocker>();
                if (module == null)
                {
                    child.AddModule("ModuleResourceLocker");
                }
                else
                {
                    module.SetLockedVessel(vid);
                }
            }
        }

        private IEnumerable<ModuleResourceLocker> _getAllChildModules()
        {
            var children = this._getAllChildren();
            var modules = (from child in children
                           let module = child.FindModuleImplementing<ModuleResourceLocker>()
                           where module != null
                           select module);
            return modules;
        }

        [KSPEvent(guiName = "Lock all Resources", guiActiveEditor = false, guiActive = true, active = true, guiActiveUnfocused = true)]
        public void LockAll()
        {
            foreach (var module in this._getAllChildModules())
            {
                module.LockResources();
            }
        }

        [KSPEvent(guiName = "Unlock all Resources", guiActiveEditor = false, guiActive = true, active = true, guiActiveUnfocused = true)]
        public void UnlockAll()
        {
            foreach (var module in this._getAllChildModules())
            {
                module.UnlockResources();
            }
        }
    }

    public class ModuleResourceLocker : PartModule
    {
        private const byte CheckInterval = 15;
        private byte _checkCounter;
        private Guid _lockedVessel;
        private bool _resLocked;

        internal void SetLockedVessel(Guid vid)
        {
            this._lockedVessel = vid;
        }

        public void FixedUpdate()
        {
            if (this._checkCounter > 0)
            {
                this._checkCounter--;
                return;
            }
            this._checkCounter = CheckInterval;
            this._checkVesselChange();
        }

        public void Start()
        {
            this.LockResources();
        }

        internal void LockResources()
        {
            if (this._resLocked)
            {
                return;
            }
            this._resLocked = true;
            foreach (var partResource in this.part.GetResources())
            {
                partResource.flowState = false;
            }
        }

        internal void UnlockResources()
        {
            if (!this._resLocked)
            {
                return;
            }
            this._resLocked = false;
            foreach (var partResource in this.part.GetResources())
            {
                partResource.flowState = true;
            }
        }

        private void _checkVesselChange()
        {
            if (this._lockedVessel == Guid.Empty)
            {
                return;
            }
            if (this.part.vessel.id == this._lockedVessel)
            {
                return;
            }
            this.UnlockResources();
            this.part.RemoveModule(this);
        }
    }
}