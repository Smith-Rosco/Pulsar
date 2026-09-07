using System;
using System.Collections.Generic;
using System.Windows.Media;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// The visual-state module used to have zero coverage: 237 lines of centre
    /// identity + preview orchestration exercised only through a 3609-line MenuSession.
    /// These tests cover the part that used to leak into the caller — the cascade
    /// policies (ADR-024 D8 centre identity, D11 dynamic-title suppression).
    /// </summary>
    public class RadialMenuVisualStateCoordinatorTests
    {
        [Fact]
        public void UpdateVisuals_CascadeSubMenu_ShouldPreserveCentreIdentity()
        {
            var (coordinator, ctx) = Create();
            var titles = new List<string>();

            coordinator.UpdateVisuals(ctx with
            {
                ActiveSlotIndex = 0,
                MenuState = MenuState.SubMenu,
                IsCascadeSubMenu = true,
                CenterText = "Excel",
                SetDynamicTitle = titles.Add,
            });

            // D8: the centre keeps the parent Slot's identity, so it must not be
            // relabelled to "Back" just because the dismiss anchor ("0") is hovered.
            ctx.CenterSlot.Label.Should().NotBe("Back");
        }

        [Fact]
        public void UpdateVisuals_CascadeSubMenu_ShouldSuppressDynamicTitle()
        {
            var (coordinator, ctx) = Create();
            var titles = new List<string>();

            coordinator.UpdateVisuals(ctx with
            {
                ActiveSlotIndex = 0,
                MenuState = MenuState.SubMenu,
                IsCascadeSubMenu = true,
                SetDynamicTitle = titles.Add,
            });

            // D11: the dynamic title is fixed below the wheel and would overlap a
            // Ring whose parent Slot sits in the lower half.
            titles.Should().ContainSingle().Which.Should().Be(string.Empty);
        }

        [Fact]
        public void UpdateVisuals_NonCascadeSubMenu_ShouldShowBackLabel()
        {
            var (coordinator, ctx) = Create();
            var titles = new List<string>();

            coordinator.UpdateVisuals(ctx with
            {
                ActiveSlotIndex = 0,
                MenuState = MenuState.SubMenu,
                IsCascadeSubMenu = false,
                SetDynamicTitle = titles.Add,
            });

            titles.Should().ContainSingle().Which.Should().Be("Back");
            ctx.CenterSlot.Label.Should().Be("Back");
        }

        [Fact]
        public void UpdateVisuals_NoSelection_ShouldResetCentreToRootText()
        {
            var (coordinator, ctx) = Create();
            var titles = new List<string>();

            coordinator.UpdateVisuals(ctx with
            {
                ActiveSlotIndex = -1,
                MenuState = MenuState.Root,
                CenterText = "Pulsar",
                SetDynamicTitle = titles.Add,
            });

            titles.Should().ContainSingle().Which.Should().Be("Pulsar");
            ctx.CenterSlot.Label.Should().Be("Pulsar");
        }

        [Fact]
        public void UpdateVisuals_HoveredSlot_ShouldMirrorLabelOntoCentre()
        {
            var (coordinator, ctx) = Create();
            var titles = new List<string>();
            var hovered = new SlotViewModel(2, 0, 0, 40) { Label = "Run Macro", Type = SlotType.Action };

            coordinator.UpdateVisuals(ctx with
            {
                ActiveSlotIndex = 2,
                Slots = new[] { hovered },
                SetDynamicTitle = titles.Add,
            });

            ctx.CenterSlot.Label.Should().Be("Run Macro");
            titles.Should().ContainSingle().Which.Should().Be("Run Macro");
        }

        private static (RadialMenuVisualStateCoordinator Coordinator, VisualStateContext Context) Create()
        {
            var previewService = new Mock<IPreviewService>();
            var coordinator = new RadialMenuVisualStateCoordinator(previewService.Object, logger: null);

            var centerSlot = new SlotViewModel(0, 0, 0, 40) { Label = "Pulsar" };

            var context = new VisualStateContext
            {
                ActiveSlotIndex = -1,
                MenuState = MenuState.Root,
                CenterText = "Pulsar",
                Slots = Array.Empty<SlotViewModel>(),
                CenterSlot = centerSlot,
                IsCascadeSubMenu = false,
                GetPreviewHostContext = () => new PreviewHostContext(IntPtr.Zero, default),
                SetDynamicTitle = _ => { },
                SetCenterPreview = _ => { },
            };

            return (coordinator, context);
        }
    }
}
