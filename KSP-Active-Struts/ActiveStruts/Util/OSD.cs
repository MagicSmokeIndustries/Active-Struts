namespace ActiveStruts.Util
{
    public static class OSD
    {
        private const string PREFIX = "[ActiveStruts] ";

        public static void PostMessage(string text, float shownFor = 3.7f)
        {
            CIT_Util.OSD.PostMessageUpperCenter(PREFIX + text, shownFor);
        }

        public static void PostMessageLowerRightCorner(string text, float shownFor = 1f)
        {
            CIT_Util.OSD.PostMessageLowerRightCorner(PREFIX + text, shownFor);
        }
    }
}