namespace Pulsar.Core.Messages
{
    public class OpenSettingsMessage
    {
        public string ProfileName { get; }
        public string ViewName { get; }

        public OpenSettingsMessage(string profileName, string viewName)
        {
            ProfileName = profileName;
            ViewName = viewName;
        }
    }
}
