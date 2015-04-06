using UnityEngine;

namespace CIT_Util.Converter
{
    internal static class ConvUtil
    {
        internal const double Epsilon = 0.00000001d;
        internal const string NaString = "n.a.";
        internal const string ElectricCharge = "ElectricCharge";
        internal const double ElectricChargeMaxDelta = 1d;
        internal const double MaxDelta = 60*60*6;
        internal const double RetryDeltaThreshold = 60d;

        private static void Log(string msg, bool warning, bool error)
        {
            const string prefix = "[CIT_UConv] ";
            var text = prefix + msg;
            if (warning)
            {
                Debug.LogWarning(text);
            }
            else if (error)
            {
                Debug.LogError(text);
            }
            else
            {
                Debug.Log(text);
            }
        }

        internal static void Log(string msg)
        {
            Log(msg, false, false);
        }

        internal static void LogError(string msg)
        {
            Log(msg, false, true);
        }

        internal static void LogWarning(string msg)
        {
            Log(msg, true, false);
        }
    }
}