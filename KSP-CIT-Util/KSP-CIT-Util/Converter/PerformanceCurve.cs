using System.Collections.Generic;

namespace CIT_Util.Converter
{
    internal class PerformanceCurve
    {
        private readonly SortedList<int, PerformanceCurvePoint> _points;

        internal PerformanceCurve(ICollection<PerformanceCurvePoint> points)
        {
            this._points = new SortedList<int, PerformanceCurvePoint>(points.Count);
            foreach (var performanceCurvePoint in points)
            {
                this._points.Add(performanceCurvePoint.Priority, performanceCurvePoint);
            }
        }

        internal PerformanceAdjustmentRatios GetRatios(double minInputReserve, double minOutputReserve)
        {
            foreach (var performanceCurvePoint in this._points.Values)
            {
                if (performanceCurvePoint.InputMin <= minInputReserve && performanceCurvePoint.OutputMin >= minOutputReserve)
                {
                    return performanceCurvePoint;
                }
            }
            //foreach (var performanceCurvePoint in this._points.Values)
            //{
            //    if (performanceCurvePoint.OutputMin >= minOutputReserve)
            //    {
            //        return performanceCurvePoint;
            //    }
            //}
            //foreach (var performanceCurvePoint in this._points.Values)
            //{
            //    if (performanceCurvePoint.InputMin <= minInputReserve)
            //    {
            //        return performanceCurvePoint;
            //    }
            //}
            //ConvUtil.LogWarning("unable to find matching perf. curve rule, using default 100% ratios.");
            return PerformanceAdjustmentRatios.Default;
        }
    }

    internal class PerformanceAdjustmentRatios
    {
        internal double InputRatio;
        internal double OutputRatio;

        internal static PerformanceAdjustmentRatios Default
        {
            get { return new PerformanceAdjustmentRatios {InputRatio = 1d, OutputRatio = 1d}; }
        }
    }

    internal class PerformanceCurvePoint : PerformanceAdjustmentRatios
    {
        internal double InputMin;
        internal double OutputMin;
        internal int Priority;

        internal PerformanceCurvePoint(int priority, double inRatio, double outRatio, double inmin, double outmin)
        {
            this.InputRatio = inRatio;
            this.OutputRatio = outRatio;
            this.InputMin = inmin;
            this.OutputMin = outmin;
            this.Priority = priority;
        }
    }
}