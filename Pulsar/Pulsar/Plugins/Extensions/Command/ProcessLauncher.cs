using System.Diagnostics;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.Extensions.Command
{
    public class ProcessLauncher : IProcessLauncher
    {
        public void Launch(ProcessStartInfo startInfo)
        {
            Process.Start(startInfo);
        }
    }
}
