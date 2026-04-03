using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class GlobalMouseWheelService : IGlobalMouseWheelService
    {
        private readonly GlobalMouseWheelHook _hook;
        private bool _isInitialized;

        public GlobalMouseWheelService(GlobalMouseWheelHook hook)
        {
            _hook = hook;
        }

        public event GlobalMouseWheelHook.GlobalMouseWheelEventHandler? OnMouseWheel;

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _hook.OnMouseWheel += HandleMouseWheel;
            _isInitialized = true;
        }

        private void HandleMouseWheel(ref GlobalMouseWheelEvent e)
        {
            OnMouseWheel?.Invoke(ref e);
        }
    }
}
