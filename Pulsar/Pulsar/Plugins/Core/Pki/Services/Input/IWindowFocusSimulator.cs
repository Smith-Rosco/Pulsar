using System;
using System.Threading.Tasks;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public interface IWindowFocusSimulator
    {
        Task ReturnFocusAsync(IntPtr hwnd);
    }
}