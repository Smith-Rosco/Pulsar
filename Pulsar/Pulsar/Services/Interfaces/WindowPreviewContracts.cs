using System;
using System.Windows;
using System.Windows.Media;

namespace Pulsar.Services.Interfaces
{
    public enum WindowPreviewKind
    {
        Icon = 0,
        Snapshot = 1,
        Live = 2
    }

    public sealed class ResolvedWindowPreview
    {
        private ResolvedWindowPreview(WindowPreviewKind kind, ImageSource? image)
        {
            Kind = kind;
            Image = image;
        }

        public WindowPreviewKind Kind { get; }

        public ImageSource? Image { get; }

        public bool HasPreviewVisual => Kind == WindowPreviewKind.Live || Image != null;

        public static ResolvedWindowPreview Live()
        {
            return new ResolvedWindowPreview(WindowPreviewKind.Live, null);
        }

        public static ResolvedWindowPreview Snapshot(ImageSource image)
        {
            return new ResolvedWindowPreview(WindowPreviewKind.Snapshot, image);
        }

        public static ResolvedWindowPreview Icon(ImageSource? image)
        {
            return new ResolvedWindowPreview(WindowPreviewKind.Icon, image);
        }
    }

    public readonly record struct PreviewHostContext(IntPtr HostWindowHandle, Rect DestinationRect)
    {
        public bool IsUsable => HostWindowHandle != IntPtr.Zero && !DestinationRect.IsEmpty && DestinationRect.Width > 0 && DestinationRect.Height > 0;
    }

    public readonly record struct PreviewHostSurface(IntPtr WindowHandle, Rect DestinationRect)
    {
        public bool IsUsable => WindowHandle != IntPtr.Zero && !DestinationRect.IsEmpty && DestinationRect.Width > 0 && DestinationRect.Height > 0;
    }
}
