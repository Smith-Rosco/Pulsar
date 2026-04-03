using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Pulsar.Services.Interfaces
{
    public interface IPreviewService
    {
        Task<BitmapSource?> CaptureAsync(IntPtr hWnd);
        void InvalidateCache(IntPtr hWnd);
        void ClearCache();
    }
}
