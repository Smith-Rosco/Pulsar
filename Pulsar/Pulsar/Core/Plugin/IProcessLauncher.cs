using System.Diagnostics;

namespace Pulsar.Core.Plugin
{
    public interface IProcessLauncher
    {
        void Launch(ProcessStartInfo startInfo);
    }
}
