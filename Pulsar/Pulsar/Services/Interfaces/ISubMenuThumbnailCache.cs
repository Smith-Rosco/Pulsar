using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Pulsar.Services.Interfaces
{
    public interface ISubMenuThumbnailCache
    {
        ImageSource? Get(IntPtr hWnd);
        Task<ImageSource?> GetOrCaptureAsync(IntPtr hWnd, string windowTitle);
        void Invalidate(IntPtr hWnd);
        void Clear();
    }
}
