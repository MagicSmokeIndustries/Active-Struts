using ActiveStruts.Util;

namespace ActiveStruts.Modules
{
    public class ModuleKerbalHookAnchor : PartModule
    {
        private const string ATTACH_TETHER_LABEL = "AttachTether";
        private const string RELEASE_TETHER_LABEL = "ReleaseTether";
        internal bool IsConnected = false;
        internal ModuleKerbalHook KerbalHook = null;

        public void Update()
        {
            var ate = Events[ATTACH_TETHER_LABEL];
            var rte = Events[RELEASE_TETHER_LABEL];
            if (HighLogic.LoadedSceneIsFlight
                && ate != null
                && rte != null
                && FlightGlobals.ActiveVessel != null
                && FlightGlobals.ActiveVessel.isEVA
                && !Config.Instance.EnableFreeAttachKerbalTether)
            {
                if (IsConnected)
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

        [KSPEvent(name = ATTACH_TETHER_LABEL, active = false, guiActive = true, guiName = "Attach Tether",
            guiActiveEditor = false, guiActiveUnfocused = true)]
        public void AttachTether()
        {
            if (IsConnected)
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

        [KSPEvent(name = RELEASE_TETHER_LABEL, active = false, guiActive = true, guiName = "Release Tether",
            guiActiveEditor = false, guiActiveUnfocused = true)]
        public void ReleaseTether()
        {
            if (KerbalHook != null)
            {
                KerbalHook.ReleaseHookAnchor();
            }
            else
            {
                IsConnected = false;
            }
        }
    }
}