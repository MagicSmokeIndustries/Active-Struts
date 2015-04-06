namespace ActiveStruts.Util
{
    public static class OSD
    {
        private const string Prefix = "[ActiveStruts] ";

        public static void PostMessage(string text, float shownFor = 3.7f)
        {
            CIT_Util.OSD.PostMessageUpperCenter(Prefix + text, shownFor);
        }

        public static void PostMessageLowerRightCorner(string text, float shownFor = 1f)
        {
            CIT_Util.OSD.PostMessageLowerRightCorner(Prefix + text, shownFor);
        }
    }
}