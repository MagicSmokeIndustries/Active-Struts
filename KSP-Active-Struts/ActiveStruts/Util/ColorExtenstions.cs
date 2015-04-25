using UnityEngine;

namespace ActiveStruts.Util
{
    public static class ColorExtenstions
    {
        public static Color MakeColorTransparent(this Color color, float transparency)
        {
            var rgba = color.GetRgba();
            return new Color(rgba[0], rgba[1], rgba[2], transparency);
        }

        public static float[] GetRgba(this Color color)
        {
            var ret = new float[4];
            ret[0] = color.r;
            ret[1] = color.g;
            ret[2] = color.b;
            ret[3] = color.a;
            return ret;
        }
    }
}
