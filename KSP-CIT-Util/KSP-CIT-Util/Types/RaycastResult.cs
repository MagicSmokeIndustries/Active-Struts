using UnityEngine;

namespace CIT_Util.Types
{
    public class RaycastResult
    {
        public float DistanceFromOrigin { get; set; }
        public RaycastHit Hit { get; set; }
        public bool HitResult { get; set; }
        public Part HittedPart { get; set; }
        public Ray Ray { get; set; }
        public float RayAngle { get; set; }
    }
}