using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Pulsar.Services.Interfaces
{
    public interface IPreviewService
    {
        Task<ResolvedWindowPreview> ResolvePreviewAsync(IntPtr hWnd, ImageSource? icon, PreviewHostContext hostContext);
        void InvalidateCache(IntPtr hWnd);
        void ClearLivePreview();
        void ClearCache();
    }
}
