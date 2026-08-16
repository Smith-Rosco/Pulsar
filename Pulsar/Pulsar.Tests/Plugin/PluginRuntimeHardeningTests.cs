using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    public class PluginRuntimeHardeningTests
    {
        [Fact]
        public void PluginExecutionContext_NestedScopes_ShouldRestorePreviousScope()
        {
            using var outer = PluginExecutionContext.BeginScope("outer.plugin", "outer-action");
            var outerId = outer.ExecutionId;

            using (var inner = PluginExecutionContext.BeginScope("inner.plugin", "inner-action"))
            {
                PluginExecutionContext.Current.Should().BeSameAs(inner);
                PluginExecutionContext.Current!.ExecutionId.Should().NotBe(outerId);
            }

            PluginExecutionContext.Current.Should().BeSameAs(outer,
                "disposing an inner scope must restore the outer AsyncLocal scope");
            PluginExecutionContext.Current!.PluginId.Should().Be("outer.plugin");
        }

        [Fact]
        public void CircuitBreaker_FailuresOutsideWindow_ShouldNotTrip()
        {
            var policy = new PluginCircuitBreakerPolicy();
            var descriptor = CreateExtensionDescriptor("test.windowed.plugin");
            var exception = new InvalidOperationException("boom");

            policy.RecordFailure(descriptor, descriptor.Id, exception);
            policy.RecordFailure(descriptor, descriptor.Id, exception);

            SetFailureTimestamps(policy, descriptor.Id, DateTime.UtcNow - TimeSpan.FromMinutes(2));

            // The first new failure evicts both old timestamps, leaving count=1.
            policy.RecordFailure(descriptor, descriptor.Id, exception);
            var availability = policy.CheckAvailability(descriptor, descriptor.Id);
            availability.Allowed.Should().BeTrue(
                "failures older than the one-minute window must not contribute to the breaker");
        }

        [Fact]
        public void CircuitBreaker_ThreeFailuresInsideWindow_ShouldTrip()
        {
            var policy = new PluginCircuitBreakerPolicy();
            var descriptor = CreateExtensionDescriptor("test.windowed.trip.plugin");
            var exception = new InvalidOperationException("boom");

            policy.RecordFailure(descriptor, descriptor.Id, exception);
            policy.RecordFailure(descriptor, descriptor.Id, exception);
            policy.RecordFailure(descriptor, descriptor.Id, exception);

            var availability = policy.CheckAvailability(descriptor, descriptor.Id);
            availability.Allowed.Should().BeFalse(
                "three failures inside the window should open the circuit");
        }

        [Fact]
        public void PluginRuntimeStateStore_ShouldRejectInvalidTransitions()
        {
            var store = new PluginRuntimeStateStore();

            var invalidTransition = () => store.Transition("missing.plugin", PluginLifecycleState.Running);

            invalidTransition.Should().Throw<InvalidOperationException>(
                "Unloaded -> Running is not a legal transition");
        }

        private static void SetFailureTimestamps(
            PluginCircuitBreakerPolicy policy,
            string pluginId,
            DateTime timestamp)
        {
            var field = typeof(PluginCircuitBreakerPolicy).GetField(
                "_recentFailures",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull("the test must stay aligned with the policy implementation");

            var failures = field!.GetValue(policy) as ConcurrentDictionary<string, List<DateTime>>;
            failures.Should().NotBeNull();
            failures!.TryGetValue(pluginId, out var timestamps).Should().BeTrue();

            lock (timestamps!)
            {
                for (var i = 0; i < timestamps.Count; i++)
                {
                    timestamps[i] = timestamp;
                }
            }
        }

        private static PluginDescriptor CreateExtensionDescriptor(string pluginId)
        {
            return new PluginDescriptor
            {
                Id = pluginId,
                DisplayName = pluginId,
                Version = "1.0.0",
                Author = "Test",
                Description = "Test descriptor",
                Icon = "T",
                CanDisable = true,
                Tier = PluginTier.Extension,
                ImplementationType = typeof(PluginRuntimeHardeningTests),
                Dependencies = new List<string>(),
                Metadata = new PluginMetadata
                {
                    Id = pluginId,
                    Display = new DisplayInfo
                    {
                        Name = pluginId,
                        Description = "Test descriptor",
                        IconKey = "T",
                        Category = "Tests",
                        Version = "1.0.0",
                        Author = "Test",
                        License = "MIT"
                    },
                    Schema = null,
                    UI = new UIHints
                    {
                        Badge = "Test",
                        AccentColor = "#4A90E2",
                        ShowInQuickAccess = false,
                        SortOrder = 0
                    },
                    Capabilities = new PluginCapabilities
                    {
                        SupportedActions = new List<string>(),
                        Dependencies = new List<string>(),
                        Tier = PluginTier.Extension,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }
    }
}
