using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ActiveStruts.Util;

namespace ActiveStruts.Modules
{
    public class ModuleKerbalHookAnchor : PartModule
    {
        private const string AttachTetherLabel = "AttachTether";
        private const string ReleaseTetherLabel = "ReleaseTether";
        internal bool IsConnected = false;
        internal ModuleKerbalHook KerbalHook = null;

        public void Update()
        {
            var ate = this.Events[AttachTetherLabel];
            var rte = this.Events[ReleaseTetherLabel];
            if (HighLogic.LoadedSceneIsFlight
                && ate != null
                && rte != null
                && FlightGlobals.ActiveVessel != null
                && FlightGlobals.ActiveVessel.isEVA
                && !Config.Instance.EnableFreeAttachKerbalTether)
            {
                if (this.IsConnected)
                {
                    rte.active = rte.guiActive = rte.guiActiveUnfocused = true;
                    ate.active = ate.guiActive = ate.guiActiveUnfocused = false;
                }
                else
                {
                    rte.active = rte.guiActive = rte.guiActiveUnfocused = false;
                    ate.active = ate.guiActive = ate.guiActiveUnfocused = true;
                }
                return;
            }
            if (rte != null && ate != null)
            {
                rte.active = rte.guiActive = rte.guiActiveUnfocused = false;
                ate.active = ate.guiActive = ate.guiActiveUnfocused = false;
            }
        }

        [KSPEvent(name = AttachTetherLabel, active = false, guiActive = true, guiName = "Attach Tether", guiActiveEditor = false, guiActiveUnfocused = true)]
        public void AttachTether()
        {
            if (this.IsConnected)
            {
                return;
            }
            var av = FlightGlobals.ActiveVessel;
            if (av == null || !av.isEVA)
            {
                return;
            }
            var module = av.rootPart.FindModuleImplementing<ModuleKerbalHook>();
            if (module == null)
            {
                return;
            }
            module.SetHookAnchor(this);
        }

        [KSPEvent(name = ReleaseTetherLabel, active = false, guiActive = true, guiName = "Release Tether", guiActiveEditor = false, guiActiveUnfocused = true)]
        public void ReleaseTether()
        {
            if (this.KerbalHook != null)
            {
                this.KerbalHook.ReleaseHookAnchor();
            }
            else
            {
                this.IsConnected = false;
            }
        }
    }
}