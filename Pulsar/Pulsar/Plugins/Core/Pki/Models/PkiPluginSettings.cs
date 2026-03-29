namespace Pulsar.Plugins.Core.Pki.Models
{
    public class PkiPluginSettings
    {
        public bool AutoSubmit { get; set; }
        public int InjectionDelay { get; set; } = 50;
        public bool UseUiaFirst { get; set; } = true;
    }
}
