using System;
using Microsoft.Extensions.Logging;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class GlobalMouseService : IGlobalMouseService, IDisposable
    {
        private readonly ILogger<GlobalMouseService> _logger;
        private readonly GlobalMouseHook _hook;
        private bool _isInitialized;

        public event EventHandler<GlobalMouseEventArgs>? OnMouseEvent;

        public event EventHandler<GlobalMouseEventArgs>? OnMouseMove;

        public GlobalMouseService(ILogger<GlobalMouseService> logger, GlobalMouseHook hook)
        {
            _logger = logger;
            _hook = hook;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            _hook.OnMouseEvent += Hook_OnMouseEvent;
            _hook.OnMouseMove += Hook_OnMouseMove;
            _isInitialized = true;
            _logger.LogInformation("GlobalMouseService initialized.");
        }

        private void Hook_OnMouseEvent(object? sender, GlobalMouseEventArgs e)
        {
            OnMouseEvent?.Invoke(this, e);
        }

        private void Hook_OnMouseMove(object? sender, GlobalMouseEventArgs e)
        {
            OnMouseMove?.Invoke(this, e);
        }

        public void ReplayRightClick()
        {
            _logger.LogDebug("[DEBUG-RDX] service ReplayRightClick -> delegating to hook");
            _hook.ReplayRightClick();
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                _hook.OnMouseEvent -= Hook_OnMouseEvent;
                _hook.OnMouseMove -= Hook_OnMouseMove;
                _hook.Dispose();
            }
        }
    }
}
