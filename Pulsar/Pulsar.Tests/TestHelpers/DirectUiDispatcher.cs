// [Path]: Pulsar.Tests/TestHelpers/DirectUiDispatcher.cs

using System;
using System.Threading.Tasks;
using Pulsar.ViewModels;

namespace Pulsar.Tests.TestHelpers
{
    /// <summary>
    /// Direct-call UI dispatcher fake shared by MenuSession / RadialMenuViewModel
    /// tests. Candidate L (architecture review 2026-09-04) consolidated the nine
    /// per-file private copies into this single helper.
    /// </summary>
    public sealed class DirectUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Invoke(Action action) => action();

        public void InvokeWithInputPriority(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task BeginInvoke(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
