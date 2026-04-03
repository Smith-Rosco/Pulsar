using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public interface IGlobalMouseWheelService
    {
        void Initialize();

        event GlobalMouseWheelHook.GlobalMouseWheelEventHandler? OnMouseWheel;
    }
}
