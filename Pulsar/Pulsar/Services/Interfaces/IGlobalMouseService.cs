using System;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public interface IGlobalMouseService
    {
        void Initialize();

        event EventHandler<GlobalMouseEventArgs>? OnMouseEvent;
    }
}
