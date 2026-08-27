using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Models;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// <see cref="WindowInventoryCache"/> — the Switch-mode window snapshot cache.
    /// A warm cache must skip the full desktop enumeration, and must never serve
    /// data past its TTL or after an invalidation.
    /// </summary>
    public class WindowInventoryCacheTests
    {
        [Fact]
        public void TryGet_ReturnsStoredSnapshot_WhileFresh()
        {
            var cache = new WindowInventoryCache(ttl: TimeSpan.FromSeconds(10));
            var windows = CreateWindows("notepad");

            cache.Store(windows);
            var hit = cache.TryGet(out var result);

            hit.Should().BeTrue();
            result!.Should().HaveCount(windows.Count);
        }

        [Fact]
        public void TryGet_ReturnsFalse_WhenNothingStored()
        {
            var cache = new WindowInventoryCache();

            var hit = cache.TryGet(out var result);

            hit.Should().BeFalse();
            result.Should().BeNull();
        }

        [Fact]
        public async Task TryGet_ReturnsFalse_AfterTtlExpiry()
        {
            var cache = new WindowInventoryCache(ttl: TimeSpan.FromMilliseconds(30));
            cache.Store(CreateWindows("notepad"));

            await Task.Delay(80);

            var hit = cache.TryGet(out var result);
            hit.Should().BeFalse();
            result.Should().BeNull();
        }

        [Fact]
        public void TryGet_ReturnsFalse_AfterInvalidate()
        {
            var cache = new WindowInventoryCache(ttl: TimeSpan.FromSeconds(10));
            cache.Store(CreateWindows("notepad"));

            cache.Invalidate();

            var hit = cache.TryGet(out _);
            hit.Should().BeFalse();
        }

        [Fact]
        public void Store_OverwritesPreviousSnapshot()
        {
            var cache = new WindowInventoryCache(ttl: TimeSpan.FromSeconds(10));
            cache.Store(CreateWindows("notepad"));
            var newer = CreateWindows("chrome");

            cache.Store(newer);

            cache.TryGet(out var result);
            result!.Single().ProcessName.Should().Be("chrome");
        }

        [Fact]
        public void TryGet_ReturnsCopy_NotTheSharedReference()
        {
            var cache = new WindowInventoryCache(ttl: TimeSpan.FromSeconds(10));
            cache.Store(CreateWindows("notepad"));

            cache.TryGet(out var first);
            first!.Clear();

            // The cached snapshot must be untouched by the caller's mutation.
            cache.TryGet(out var second);
            second!.Should().HaveCount(1);
        }

        private static List<ProcessWindowInfo> CreateWindows(string processName)
        {
            return
            [
                new ProcessWindowInfo
                {
                    Handle = new IntPtr(1),
                    ProcessName = processName,
                    Title = $"{processName} window"
                }
            ];
        }
    }
}
