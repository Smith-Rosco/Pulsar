using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Rendering;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Verifies RadialMenuViewModel.ApplyRadialRendering resolves the active renderer
    /// through the StyleRendererFactory from the configured ProfileSettings.RadialRenderer,
    /// and that an unknown value falls back to the Default renderer without throwing.
    /// </summary>
    public class RadialMenuRendererSelectionTests
    {
        [Fact]
        public void ApplyRadialRendering_ConfiguredId_ShouldInitializeThatRendererOnMenuOpen()
        {
            var classic = new Mock<IRadialRenderer>();
            classic.SetupGet(r => r.Id).Returns("ClassicRing");

            var factory = new StyleRendererFactory(new IRadialRenderer[]
            {
                new DefaultRadialRenderer(),
                classic.Object
            });

            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig
            {
                Settings = new ProfileSettings { RadialRenderer = "ClassicRing" }
            });

            var vm = CreateViewModel(config, factory);

            // ApplyRadialRendering runs on construction and on menu open; the factory
            // must have resolved the configured renderer and initialized it with tokens.
            classic.Verify(r => r.Initialize(It.IsAny<IRadialThemeTokens>()), Times.AtLeastOnce);
        }

        [Fact]
        public void ApplyRadialRendering_UnknownId_ShouldFallBackToDefault_WithoutThrowing()
        {
            var defaultRenderer = new Mock<IRadialRenderer>();
            defaultRenderer.SetupGet(r => r.Id).Returns(DefaultRadialRenderer.RendererId);

            var factory = new StyleRendererFactory(new IRadialRenderer[] { defaultRenderer.Object });

            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig
            {
                Settings = new ProfileSettings { RadialRenderer = "DoesNotExist" }
            });

            // Constructing the VM runs ApplyRadialRendering; an unknown value must not
            // throw and must feed the Default renderer.
            var vm = CreateViewModel(config, factory);

            defaultRenderer.Verify(r => r.Initialize(It.IsAny<IRadialThemeTokens>()), Times.AtLeastOnce);
        }

        [Fact]
        public void ConfigUpdated_ChangedRendererStyle_ShouldReApplyWithNewRendererOnNextOpen()
        {
            // Saving the renderer selector triggers ConfigUpdated; the VM must
            // re-resolve the renderer via the factory and initialize the newly
            // selected one on the next menu open.
            var classic = new Mock<IRadialRenderer>();
            classic.SetupGet(r => r.Id).Returns("ClassicRing");
            var glass = new Mock<IRadialRenderer>();
            glass.SetupGet(r => r.Id).Returns("Glassmorphism");

            var factory = new StyleRendererFactory(new IRadialRenderer[]
            {
                new DefaultRadialRenderer(),
                classic.Object,
                glass.Object
            });

            var snapshot = new ProfilesConfig
            {
                Settings = new ProfileSettings { RadialRenderer = "ClassicRing" }
            };
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(snapshot);

            var vm = CreateViewModel(config, factory);
            classic.Verify(r => r.Initialize(It.IsAny<IRadialThemeTokens>()), Times.AtLeastOnce);

            // User changes the selector to Glassmorphism in settings, then saves.
            // Invoke the same re-render seam OnConfigUpdated runs on the next open,
            // bypassing Application.Current.Dispatcher (which sibling tests may own).
            snapshot.Settings.RadialRenderer = "Glassmorphism";
            InvokeApplyRadialRendering(vm);

            glass.Verify(r => r.Initialize(It.IsAny<IRadialThemeTokens>()), Times.AtLeastOnce,
                "the re-render must resolve the new renderer through the factory and initialize it");
        }

        private static void InvokeApplyRadialRendering(RadialMenuViewModel vm)
        {
            var method = typeof(RadialMenuViewModel).GetMethod(
                "ApplyRadialRendering",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method!.Invoke(vm, new object[] { vm.CurrentMode });
        }

        private static RadialMenuViewModel CreateViewModel(Mock<IConfigService> config, StyleRendererFactory factory)
        {
            var session = CreateSession();

            var hotkey = new Mock<IHotkeyService>();
            var globalMouse = new Mock<IGlobalMouseService>();
            var mouseTracking = new Mock<IMouseTrackingService>();
            var viewport = new Mock<IMenuViewportService>();

            var presetResolver = new RadialThemePresetResolver(
                logger: null,
                systemThemeProvider: () => AppTheme.Light,
                builtInFactory: _ => CreateTokens());

            return new RadialMenuViewModel(
                session,
                hotkey.Object,
                globalMouse.Object,
                mouseTracking.Object,
                viewport.Object,
                config.Object,
                new Mock<ILocalizationService>().Object,
                logger: null,
                rendererFactory: factory,
                presetResolver: presetResolver);
        }

        private static MenuSession CreateSession()
        {
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, 0, 8));
            slotLayoutEngine
                .Setup(engine => engine.GetSlotPosition(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<LayoutParameters>()))
                .Returns((0d, 0d));
            slotLayoutEngine
                .Setup(engine => engine.HitTest(It.IsAny<Vector>(), It.IsAny<LayoutParameters>()))
                .Returns(-1);

            var animationController = new Mock<IAnimationController>();
            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configService = new Mock<IConfigService>();
            configService.Setup(service => service.GetValidatedSlotsPerPage()).Returns(8);
            configService.Setup(service => service.GetSnapshot()).Returns(new ProfilesConfig());

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                Mock.Of<IWindowService>(),
                Mock.Of<IWindowInventoryCoordinator>(),
                new Mock<IHotkeyService>().Object,
                Mock.Of<ITrayService>(),
                animationController.Object,
                slotLayoutEngine.Object,
                Mock.Of<IPagingController>(),
                Mock.Of<IPreviewService>(),
                Mock.Of<IPageProviderFactory>(),
                loc.Object,
                new DirectUiDispatcher());

            session.Initialize();
            return session;
        }

        private static IRadialThemeTokens CreateTokens()
        {
            return new RadialThemeTokenSet(
                orbFill: Brushes.Gray,
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
}
