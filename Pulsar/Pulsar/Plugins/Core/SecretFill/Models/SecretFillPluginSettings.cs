namespace Pulsar.Plugins.Core.SecretFill.Models
{
    public class SecretFillPluginSettings
    {
        public bool AutoSubmit { get; set; }
        public int InjectionDelay { get; set; } = 50;
    }
}
