using System;
using System.Threading.Tasks;

namespace Pulsar.Plugins.Core.Pki.Contracts
{
    public interface IFocusRestorer
    {
        Task<bool> RestoreFocusAsync(IntPtr hwnd);
    }
}
