using UnityEngine;

namespace ActiveStruts.Util
{
    /*
     * This class is based on code from Kethane by Majiir and the following license applies:
     * 
     * Copyright © Majiir 2012-2014
     * All rights reserved.
     * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
     * 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
     * 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
     * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
     */

    internal static class RotationAngleHelper
    {
        private static Vector2 CartesianToPolar(Vector3 point)
        {
            var polar = new Vector2 {y = Mathf.Atan2(point.x, point.z)};
            var xzLen = new Vector2(point.x, point.z).magnitude;
            polar.x = Mathf.Atan2(-point.y, xzLen);
            polar *= Mathf.Rad2Deg;
            return polar;
        }

        internal static float GetYRotationAngleToLookAtTarget(Transform origin, Transform target)
        {
            var targetWorld = CartesianToPolar(origin.InverseTransformPoint(target.position));
            var angle = (float) NormalizeAngle(targetWorld.y);
            return angle;
        }

        private static double NormalizeAngle(double a)
        {
            a = a%360;
            if (a < 0)
            {
                a += 360;
            }
            return a;
        }
    }
}