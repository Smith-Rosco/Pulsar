using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Window snapshot capture and executable icon extraction. Consumers that only
    /// need a thumbnail (previews, sub-menu thumbnails) or an icon should depend on
    /// this narrow seam instead of the full <see cref="IWindowService"/>.
    /// </summary>
    public interface IWindowCaptureService
    {
        /// <summary>
        /// 捕获指定窗口的静态快照（PrintWindow + 缩放冻结为 <see cref="ImageSource"/>）。
        /// 无效句柄 / 不可捕获时返回 null，不抛出。
        /// </summary>
        Task<ImageSource?> CaptureWindowAsync(IntPtr hwnd);

        /// <summary>
        /// 提取可执行文件的图标（带内存缓存；失败路径缓存 null 以免反复重试）。
        /// </summary>
        ImageSource? ExtractIcon(string path);
    }
}
