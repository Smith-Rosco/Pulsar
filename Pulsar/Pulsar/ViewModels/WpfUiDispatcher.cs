using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// WPF adapter over <see cref="Dispatcher"/> for the MenuSession dispatcher seam.
    /// Keeps MenuSession free of direct <see cref="Application"/> references so tests
    /// can inject a direct-call fake.
    /// </summary>
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            return Application.Current?.Dispatcher.CheckAccess() != false;
        }

        public void Invoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public Task InvokeAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        public Task BeginInvoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.BeginInvoke(action, DispatcherPriority.Input).Task;
        }
    }
}
