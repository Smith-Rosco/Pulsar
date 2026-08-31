using System;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public interface IGlobalMouseService
    {
        void Initialize();

        event EventHandler<GlobalMouseEventArgs>? OnMouseEvent;

        /// <summary>
        /// Raised for low-level <c>WM_MOUSEMOVE</c> with cursor screen coordinates.
        /// Opt-in and frequently firing; subscribers (the right-drag displacement
        /// tracker) must keep the handler cheap.
        /// </summary>
        event EventHandler<GlobalMouseEventArgs>? OnMouseMove;

        /// <summary>
        /// Synthesizes a right-button down+up at the current cursor position so a
        /// sub-threshold right-drag release reaches the source application's native
        /// context menu. The hook suppresses its own replayed events.
        /// </summary>
        void ReplayRightClick();
    }
}
