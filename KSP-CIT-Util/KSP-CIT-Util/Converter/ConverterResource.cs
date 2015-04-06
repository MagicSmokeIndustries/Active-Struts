using System;

namespace CIT_Util.Converter
{
    internal class ConverterResource
    {
        internal bool AllowOverflow { get; set; }
        internal float Density { get; set; }
        internal ResourceFlowMode FlowMode { get; set; }
        internal bool OutputResource { get; set; }
        internal double RatePerSecond { get; set; }
        internal int ResourceId { get; set; }
        internal string ResourceName { get; set; }
        internal ResourceTransferMode TransferMode { get; set; }
        internal float UnitCost { get; set; }

        internal static bool CreateNew(string resourceName, double ratePerSecond, out ConverterResource outObj, bool outputRes = false, bool allowOverflow = false)
        {
            try
            {
                var resDef = PartResourceLibrary.Instance.GetDefinition(resourceName);
                if (resDef != null)
                {
                    var cr = new ConverterResource
                             {
                                 ResourceName = resourceName,
                                 ResourceId = resDef.id,
                                 RatePerSecond = ratePerSecond,
                                 FlowMode = resDef.resourceFlowMode,
                                 OutputResource = outputRes,
                                 AllowOverflow = allowOverflow,
                                 Density = resDef.density,
                                 UnitCost = resDef.unitCost
                             };
                    outObj = cr;
                    return true;
                }
            }
            catch (Exception)
            {
                ConvUtil.LogError("unable to find resource " + resourceName + " - ignoring resource definition");
            }
            outObj = null;
            return false;
        }
    }
}