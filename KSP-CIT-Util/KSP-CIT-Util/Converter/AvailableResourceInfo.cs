using System;

namespace CIT_Util.Converter
{
    internal class AvailableResourceInfo
    {
        internal AvailableResourceInfo(int id, bool outres, double available, double availSpace, double ratePerSec, bool allowOverflow = false)
        {
            this.ResourceId = id;
            this.OutputResource = outres;
            this.AvailableAmount = available;
            this.AvailableSpace = availSpace;
            this.TotalSpace = this.AvailableAmount + this.AvailableSpace;
            this.PercentageFilled = this.AvailableAmount/this.TotalSpace;
            this.RatePerSecond = ratePerSec;
            this.AllowOverflow = allowOverflow;
        }

        internal bool AllowOverflow { get; set; }
        internal double AvailableAmount { get; set; }
        internal double AvailableSpace { get; set; }
        internal bool OutputResource { get; set; }
        internal double PercentageFilled { get; set; }
        internal double RatePerSecond { get; set; }
        internal int ResourceId { get; set; }
        internal double TotalSpace { get; set; }

        internal bool CanTakeDemand(double demand)
        {
            double diff;
            if (this.OutputResource)
            {
                if (this.AllowOverflow)
                {
                    return true;
                }
                diff = this.AvailableSpace - demand;
            }
            else
            {
                diff = this.AvailableAmount - demand;
            }
            return diff > 0 && diff > ConvUtil.Epsilon;
        }

        internal int TimesCanTakeDemand(double demand)
        {
            double times;
            if (this.OutputResource)
            {
                if (this.AllowOverflow)
                {
                    return int.MaxValue;
                }
                times = this.AvailableSpace/demand;
            }
            else
            {
                times = this.AvailableAmount/demand;
            }
            return (int) Math.Floor(times);
        }
    }
}