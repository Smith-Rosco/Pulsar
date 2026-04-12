using System;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    internal interface ILiveWindowPreviewHost
    {
        bool TryShowPreview(IntPtr sourceWindowHandle, PreviewHostContext hostContext);

        void Clear();
    }

    internal sealed class LiveWindowPreviewHost : ILiveWindowPreviewHost
    {
        private IntPtr _registeredThumbnail = IntPtr.Zero;
        private IntPtr _hostWindowHandle = IntPtr.Zero;
        private IntPtr _sourceWindowHandle = IntPtr.Zero;
        private IntPtr _surfaceWindowHandle = IntPtr.Zero;

        public bool TryShowPreview(IntPtr sourceWindowHandle, PreviewHostContext hostContext)
        {
            if (sourceWindowHandle == IntPtr.Zero || !hostContext.IsUsable)
            {
                Clear();
                return false;
            }

            var surface = EnsureSurface(hostContext);
            if (!surface.IsUsable)
            {
                Clear();
                return false;
            }

            if (!EnsureThumbnail(sourceWindowHandle, surface.WindowHandle))
            {
                Clear();
                return false;
            }

            if (DwmHelper.DwmQueryThumbnailSourceSize(_registeredThumbnail, out var size) != 0 || size.x <= 0 || size.y <= 0)
            {
                Clear();
                return false;
            }

            var destination = surface.DestinationRect;
            var dpi = PulsarNative.GetDpiForWindow(surface.WindowHandle);
            var dpiScale = dpi > 0 ? dpi / 96.0 : 1.0;

            var sourceAspect = (double)size.x / size.y;
            var destinationAspect = destination.Width / destination.Height;

            double renderWidth = destination.Width;
            double renderHeight = destination.Height;
            double renderLeft = 0;
            double renderTop = 0;

            if (sourceAspect > destinationAspect)
            {
                renderWidth = destination.Height * sourceAspect;
                renderLeft = -(renderWidth - destination.Width) / 2;
            }
            else
            {
                renderHeight = destination.Width / sourceAspect;
                renderTop = -(renderHeight - destination.Height) / 2;
            }

            var properties = new DwmHelper.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DwmHelper.DWM_TNP_VISIBLE |
                          DwmHelper.DWM_TNP_OPACITY |
                          DwmHelper.DWM_TNP_RECTDESTINATION |
                          DwmHelper.DWM_TNP_SOURCECLIENTAREAONLY,
                fVisible = true,
                opacity = 255,
                fSourceClientAreaOnly = false,
                rcDestination = new DwmHelper.RECT(
                    (int)Math.Round(renderLeft * dpiScale),
                    (int)Math.Round(renderTop * dpiScale),
                    (int)Math.Round((renderLeft + renderWidth) * dpiScale),
                    (int)Math.Round((renderTop + renderHeight) * dpiScale))
            };

            if (DwmHelper.DwmUpdateThumbnailProperties(_registeredThumbnail, ref properties) != 0)
            {
                Clear();
                return false;
            }

            return true;
        }

        public void Clear()
        {
            if (_registeredThumbnail != IntPtr.Zero)
            {
                DwmHelper.DwmUnregisterThumbnail(_registeredThumbnail);
            }

            _registeredThumbnail = IntPtr.Zero;
            _hostWindowHandle = IntPtr.Zero;
            _sourceWindowHandle = IntPtr.Zero;

            if (_surfaceWindowHandle != IntPtr.Zero)
            {
                PulsarNative.DestroyWindow(_surfaceWindowHandle);
                _surfaceWindowHandle = IntPtr.Zero;
            }
        }

        private PreviewHostSurface EnsureSurface(PreviewHostContext hostContext)
        {
            var destination = hostContext.DestinationRect;
            var dpi = PulsarNative.GetDpiForWindow(hostContext.HostWindowHandle);
            var dpiScale = dpi > 0 ? dpi / 96.0 : 1.0;

            int pixelLeft = (int)Math.Round(destination.Left * dpiScale);
            int pixelTop = (int)Math.Round(destination.Top * dpiScale);
            int pixelWidth = Math.Max(1, (int)Math.Round(destination.Width * dpiScale));
            int pixelHeight = Math.Max(1, (int)Math.Round(destination.Height * dpiScale));

            if (_surfaceWindowHandle == IntPtr.Zero || !PulsarNative.IsWindow(_surfaceWindowHandle) || _hostWindowHandle != hostContext.HostWindowHandle)
            {
                if (_surfaceWindowHandle != IntPtr.Zero)
                {
                    PulsarNative.DestroyWindow(_surfaceWindowHandle);
                    _surfaceWindowHandle = IntPtr.Zero;
                }

                _surfaceWindowHandle = PulsarNative.CreateWindowEx(
                    0,
                    "Static",
                    null,
                    PulsarNative.WS_CHILD | PulsarNative.WS_VISIBLE,
                    pixelLeft,
                    pixelTop,
                    pixelWidth,
                    pixelHeight,
                    hostContext.HostWindowHandle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (_surfaceWindowHandle == IntPtr.Zero)
                {
                    return default;
                }
            }
            else
            {
                PulsarNative.SetWindowPos(
                    _surfaceWindowHandle,
                    IntPtr.Zero,
                    pixelLeft,
                    pixelTop,
                    pixelWidth,
                    pixelHeight,
                    PulsarNative.SWP_NOZORDER | PulsarNative.SWP_NOACTIVATE | PulsarNative.SWP_SHOWWINDOW);
            }

            var region = PulsarNative.CreateEllipticRgn(0, 0, pixelWidth, pixelHeight);
            if (region != IntPtr.Zero)
            {
                PulsarNative.SetWindowRgn(_surfaceWindowHandle, region, true);
            }

            _hostWindowHandle = hostContext.HostWindowHandle;
            return new PreviewHostSurface(_surfaceWindowHandle, new System.Windows.Rect(0, 0, destination.Width, destination.Height));
        }

        private bool EnsureThumbnail(IntPtr sourceWindowHandle, IntPtr hostWindowHandle)
        {
            if (_registeredThumbnail != IntPtr.Zero && _sourceWindowHandle == sourceWindowHandle && _hostWindowHandle == hostWindowHandle)
            {
                return true;
            }

            Clear();

            if (DwmHelper.DwmRegisterThumbnail(hostWindowHandle, sourceWindowHandle, out var thumbnailHandle) != 0 || thumbnailHandle == IntPtr.Zero)
            {
                return false;
            }

            _registeredThumbnail = thumbnailHandle;
            _hostWindowHandle = hostWindowHandle;
            _sourceWindowHandle = sourceWindowHandle;
            return true;
        }
    }
}
