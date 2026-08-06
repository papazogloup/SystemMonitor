namespace SystemMonitor
{
    public static class Extensions
    {
        public static void SetChecked(this CheckBox? checkBox, bool value)
        {
            if (checkBox != null) checkBox.Checked = value;
        }
    }
}