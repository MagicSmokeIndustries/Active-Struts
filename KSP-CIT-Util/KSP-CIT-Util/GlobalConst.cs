namespace CIT_Util
{
    public static class GlobalConst
    {
        public const ControlTypes GUIWindowLockMask = ControlTypes.CAMERACONTROLS | ControlTypes.MAP | ControlTypes.ACTIONS_ALL | ControlTypes.KSC_FACILITIES | ControlTypes.EVA_INPUT;
        public const ControlTypes GUIWindowLockMaskEditor = ControlTypes.EDITOR_LOCK | ControlTypes.EDITOR_SOFT_LOCK;
    }
}