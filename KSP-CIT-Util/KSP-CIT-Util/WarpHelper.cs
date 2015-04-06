namespace CIT_Util
{
    public class WarpHelper
    {
        private WarpMode _mode;
        private double _targetUt;
        private readonly GameScenes[] _allowedScenes = {GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT};
        private readonly bool _initialized;

        public WarpHelper()
        {
            if (HighLogic.LoadedSceneIsEditor || !HighLogic.LoadedSceneHasPlanetarium || !HighLogic.LoadedScene.In(this._allowedScenes))
            {
                this._initialized = false;
            }
            else
            {
                this._initialized = true;
            }
            this._mode = WarpMode.None;
        }

        public bool IsReady
        {
            get { return this._initialized; }
        }

        public void CallOnFixedUpdate()
        {
            if (!this._initialized || this._mode == WarpMode.None)
            {
                return;
            }
            switch (this._mode)
            {
                case WarpMode.ToUt:
                {
                    this._processWarpToUt();
                }
                    break;
            }
        }

        private void _processWarpToUt()
        {
            //rates = [1,2,3,4,5,10,50,100,1000,10000,100000]           
            const double kerbinDaySecs = 21600d;
            const double twoKerbinDaysSec = kerbinDaySecs*2d;
            const double halfKerbinDaySecs = kerbinDaySecs/2d;
            const double quarterKerbinDaySecs = halfKerbinDaySecs/2d;
            var now = Planetarium.GetUniversalTime();
            var diff = this._targetUt - now;
            var maxWarpIdx = TimeWarp.WarpMode == TimeWarp.Modes.LOW
                                 ? 4
                                 : diff > twoKerbinDaysSec
                                       ? 10
                                       : diff > kerbinDaySecs
                                             ? 9
                                             : diff > halfKerbinDaySecs
                                                   ? 8
                                                   : diff > quarterKerbinDaySecs
                                                         ? 7
                                                         : 6;
            if (diff <= 100d && diff > 5d)
            {
                TimeWarp.SetRate(3, true);
            }
            if (diff <= 5d)
            {
                TimeWarp.SetRate(0, true);
                this._mode = WarpMode.None;
                return;
            }
            var currRateIdx = TimeWarp.CurrentRateIndex;
            if (currRateIdx > maxWarpIdx)
            {
                TimeWarp.SetRate(currRateIdx - 1, true);
            }
            else if (currRateIdx < maxWarpIdx)
            {
                TimeWarp.SetRate(currRateIdx + 1, false);
            }
        }

        public void WarpToUt(double targetUt)
        {
            if (!this._initialized)
            {
                return;
            }
            this._targetUt = targetUt;
            this._mode = WarpMode.ToUt;
        }

        private enum WarpMode
        {
            None,
            ToUt
        }
    }
}