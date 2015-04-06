using System.Collections;
using UnityEngine;

namespace CIT_Util.Misc
{
    public class ModulePPT : PartModule
    {
        private const byte CheckInterval = 45;
        private const float ExplosionDelay = 5f;
        private byte _checkCounter;
        private bool _explosionInitiated;
        [KSPField(guiActive = true, guiName = "Explode after Decoupling", guiActiveEditor = true, isPersistant = true)] public bool ExplodeAfterDecoupled;

        private bool _hasParent
        {
            get { return this.part.parent != null; }
        }

        private bool _isDecoupled
        {
            get
            {
                var dm = this.part.FindModuleImplementing<ModuleAnchoredDecoupler>();
                return dm != null && dm.isDecoupled;
            }
        }

        public void Start()
        {
            this._checkCounter = CheckInterval;
            this._explosionInitiated = false;
        }

        public void FixedUpdate()
        {
            if (this._explosionInitiated || !HighLogic.LoadedSceneIsFlight || !this.ExplodeAfterDecoupled)
            {
                return;
            }
            if (this._checkCounter > 0)
            {
                this._checkCounter--;
                return;
            }
            this._checkCounter = CheckInterval;
            if (this._hasParent || !this._isDecoupled)
            {
                return;
            }
            this._explosionInitiated = true;
            this.StartCoroutine(this.WaitAndExplode());
        }

        private IEnumerator WaitAndExplode()
        {
            yield return new WaitForSeconds(ExplosionDelay);
            this.part.explode();
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, name = "ToggleExplode", guiName = "Toggle Explode", guiActiveUnfocused = true, unfocusedRange = 10f)]
        public void ToggleExplode()
        {
            this.ExplodeAfterDecoupled = !this.ExplodeAfterDecoupled;
        }

        [KSPAction("ToggleExplodeAction", KSPActionGroup.None, guiName = "Toggle Explode")]
        public void ToggleExplodeAction()
        {
            this.ToggleExplode();
        }
    }
}