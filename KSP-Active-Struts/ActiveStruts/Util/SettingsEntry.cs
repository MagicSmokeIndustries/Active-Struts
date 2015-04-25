namespace ActiveStruts.Util
{
    public class SettingsEntry
    {
        public SettingsEntry(object defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public object DefaultValue { get; private set; }
        public object Value { get; set; }
    }
}