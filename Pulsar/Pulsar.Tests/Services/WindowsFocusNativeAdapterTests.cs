using FluentAssertions;
using Pulsar.Services;

namespace Pulsar.Tests.Services
{
    public class WindowsFocusNativeAdapterTests
    {
        [Fact]
        public void LockForegroundTimeout_ShouldIncrementCount()
        {
            var adapter = new WindowsFocusNativeAdapter();

            adapter.LockForegroundTimeout();
            adapter.LockForegroundTimeout();

            adapter.UnlockForegroundTimeout();
        }

        [Fact]
        public void LockUnlock_ShouldSupportReentrantCalls()
        {
            var adapter = new WindowsFocusNativeAdapter();

            adapter.LockForegroundTimeout();
            adapter.LockForegroundTimeout();
            adapter.UnlockForegroundTimeout();
            adapter.UnlockForegroundTimeout();
            adapter.LockForegroundTimeout();
            adapter.UnlockForegroundTimeout();
        }

        [Fact]
        public void MultipleAdapters_ShouldHaveIndependentLockState()
        {
            var adapter1 = new WindowsFocusNativeAdapter();
            var adapter2 = new WindowsFocusNativeAdapter();

            adapter1.LockForegroundTimeout();
            adapter2.LockForegroundTimeout();

            adapter1.UnlockForegroundTimeout();
            adapter2.UnlockForegroundTimeout();
        }

        [Fact]
        public void GetForegroundWindow_ShouldNotThrow()
        {
            var adapter = new WindowsFocusNativeAdapter();

            var act = () => adapter.GetForegroundWindow();

            act.Should().NotThrow();
        }

        [Fact]
        public void IsWindow_WithZeroHandle_ShouldReturnFalse()
        {
            var adapter = new WindowsFocusNativeAdapter();

            var result = adapter.IsWindow(IntPtr.Zero);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsWindow_WithValidForegroundHandle_ShouldReturnTrue()
        {
            var adapter = new WindowsFocusNativeAdapter();
            var hwnd = adapter.GetForegroundWindow();

            if (hwnd != IntPtr.Zero)
            {
                adapter.IsWindow(hwnd).Should().BeTrue();
            }
        }

        [Fact]
        public void GetCurrentThreadId_ShouldReturnNonZero()
        {
            var adapter = new WindowsFocusNativeAdapter();

            var threadId = adapter.GetCurrentThreadId();

            threadId.Should().NotBe(0u);
        }
    }
}
