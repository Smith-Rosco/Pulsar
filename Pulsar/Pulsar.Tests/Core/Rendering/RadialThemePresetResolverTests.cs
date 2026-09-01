using System;
using System.Collections.Generic;
using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Rendering;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class RadialThemePresetResolverTests
    {
        // Captures which theme the built-in factory was asked for, so tests can assert
        // the layering without loading real XAML dictionaries.
        private sealed class CapturingFactory
        {
            public List<AppTheme> Requests { get; } = new();
            public Func<AppTheme, IRadialThemeTokens> Create()
            {
                return theme =>
                {
                    Requests.Add(theme);
                    return StubTokens(theme);
                };
            }

            private static IRadialThemeTokens StubTokens(AppTheme theme)
            {
                var color = theme == AppTheme.Dark ? Colors.DarkGray : Colors.LightGray;
                return new RadialThemeTokenSet(
                    orbFill: new SolidColorBrush(color),
                    orbStroke: Brushes.White,
                    orbText: Brushes.Black,
                    activeGlow: Brushes.Cyan,
                    labelBackground: Brushes.Black,
                    labelForeground: Brushes.White,
                    accent: Brushes.Blue,
                    accentHover: Brushes.LightBlue,
                    accentForeground: Brushes.White,
                    radialTitleForeground: Brushes.White,
                    radialTitleShadow: Brushes.Black,
                    radialTitleScrim: Brushes.Gray);
            }
        }

        [Fact]
        public void Resolve_System_ShouldFollowInjectedSystemThemeProvider()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Dark,
                builtInFactory: factory.Create());

            var tokens = resolver.Resolve("System", activeTheme: AppTheme.Light);

            tokens.Should().NotBeNull();
            factory.Requests.Should().ContainSingle(t => t == AppTheme.Dark);
        }

        [Fact]
        public void Resolve_System_ShouldFollowSystemLight_WhenProviderSaysLight()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Light,
                builtInFactory: factory.Create());

            resolver.Resolve("System", activeTheme: AppTheme.Dark);

            factory.Requests.Should().ContainSingle(t => t == AppTheme.Light);
        }

        [Fact]
        public void Resolve_NullOrEmpty_ShouldBehaveAsSystem()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Dark,
                builtInFactory: factory.Create());

            resolver.Resolve(null, activeTheme: AppTheme.Light);
            resolver.Resolve("  ", activeTheme: AppTheme.Light);

            factory.Requests.Should().HaveCount(2);
            factory.Requests.Should().OnlyContain(t => t == AppTheme.Dark);
        }

        [Fact]
        public void Resolve_Dark_ShouldUseBuiltInDarkTokens()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Light,
                builtInFactory: factory.Create());

            resolver.Resolve("Dark", activeTheme: AppTheme.Light);

            factory.Requests.Should().ContainSingle(t => t == AppTheme.Dark);
        }

        [Fact]
        public void Resolve_Light_ShouldUseBuiltInLightTokens()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Dark,
                builtInFactory: factory.Create());

            resolver.Resolve("light", activeTheme: AppTheme.Dark);

            factory.Requests.Should().ContainSingle(t => t == AppTheme.Light);
        }

        [Fact]
        public void Resolve_MatchaForest_ShouldReturnCatalogPreset_NotBuiltIn()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Dark,
                builtInFactory: factory.Create());

            var tokens = resolver.Resolve("MatchaForest", activeTheme: AppTheme.Light);

            tokens.Should().NotBeNull();
            factory.Requests.Should().BeEmpty("named presets come from the static catalog, not the built-in factory");
        }

        [Fact]
        public void Resolve_Unknown_ShouldFallBackToActiveTheme_AndNotThrow()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Dark,
                builtInFactory: factory.Create());

            var tokens = resolver.Resolve("DoesNotExist", activeTheme: AppTheme.Dark);

            tokens.Should().NotBeNull();
            factory.Requests.Should().ContainSingle(t => t == AppTheme.Dark);
        }

        [Fact]
        public void Resolve_Unknown_ShouldFallBackToLight_WhenActiveThemeIsLight()
        {
            var factory = new CapturingFactory();
            var resolver = new RadialThemePresetResolver(
                NullLogger<RadialThemePresetResolver>.Instance,
                systemThemeProvider: () => AppTheme.Dark,
                builtInFactory: factory.Create());

            resolver.Resolve("TotallyUnknown", activeTheme: AppTheme.Light);

            factory.Requests.Should().ContainSingle(t => t == AppTheme.Light);
        }
    }
}

