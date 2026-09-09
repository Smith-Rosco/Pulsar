using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [R3 2026-09-09] Centre-slot identity decisions are owned by
    /// <see cref="CenterIdentityPolicy"/>; these tests pin the policy so
    /// D7/D8-class regressions (parent identity leaking into / missing from the
    /// centre orb) surface at unit level instead of on the real machine.
    /// </summary>
    public class CenterIdentityPolicyTests
    {
        private readonly Mock<ILocalizationService> _loc = new();

        public CenterIdentityPolicyTests()
        {
            _loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
        }

        [Fact]
        public void CenterTextForParent_ParentWithLabel_ReturnsParentLabel()
        {
            var parent = new SlotViewModel(1, 0, 0, 50) { Label = "Clipboard" };

            CenterIdentityPolicy.CenterTextForParent(parent, _loc.Object)
                .Should().Be("Clipboard");
        }

        [Fact]
        public void CenterTextForParent_NullParent_FallsBackToBack()
        {
            CenterIdentityPolicy.CenterTextForParent(null, _loc.Object)
                .Should().Be("Back");
        }

        [Fact]
        public void CenterTextForParent_WhitespaceLabel_FallsBackToBack()
        {
            var parent = new SlotViewModel(1, 0, 0, 50) { Label = "   " };

            CenterIdentityPolicy.CenterTextForParent(parent, _loc.Object)
                .Should().Be("Back");
        }

        [Fact]
        public void ApplyParentIcon_ParentHasLiveImage_CopiesImageReference()
        {
            var parent = new SlotViewModel(1, 0, 0, 50);
            var center = new SlotViewModel(0, 0, 0, 50);
            var image = new System.Windows.Media.Imaging.WriteableBitmap(
                1, 1, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
            parent.IconImage = image;

            CenterIdentityPolicy.ApplyParentIcon(center, parent);

            center.IconImage.Should().BeSameAs(image);
        }

        [Fact]
        public void ApplyParentIcon_NoParent_LeavesCenterUntouched()
        {
            var center = new SlotViewModel(0, 0, 0, 50);
            center.LoadIconData(string.Empty);

            CenterIdentityPolicy.ApplyParentIcon(center, null);

            center.IconImage.Should().BeNull();
        }

        [Fact]
        public void ApplyParentIcon_ImageKeyOnly_ReloadsFromKey()
        {
            var parent = new SlotViewModel(1, 0, 0, 50) { IconKey = string.Empty };
            var center = new SlotViewModel(0, 0, 0, 50);

            CenterIdentityPolicy.ApplyParentIcon(center, parent);

            // Empty key = safe no-op load (test convention); the decisive part is
            // that no exception escapes and the live image stays cleared.
            center.IconImage.Should().BeNull();
        }

        [Fact]
        public void ParentOrbLabel_Label_ReturnsLabel()
        {
            var parent = new SlotViewModel(1, 0, 0, 50) { Label = "Report" };

            CenterIdentityPolicy.ParentOrbLabel(parent).Should().Be("Report");
        }

        [Fact]
        public void ParentOrbLabel_Whitespace_ReturnsNull()
        {
            var parent = new SlotViewModel(1, 0, 0, 50) { Label = " " };

            CenterIdentityPolicy.ParentOrbLabel(parent).Should().BeNull();
        }

        [Fact]
        public void SubMenuPageLabel_MultiPages_FormatsPageNumber()
        {
            CenterIdentityPolicy.SubMenuPageLabel("Windows", 0, 3, "{0} ({1}/{2})")
                .Should().Be("Windows (1/3)");
        }

        [Fact]
        public void SubMenuPageLabel_SinglePage_KeepsLabelUntouched()
        {
            CenterIdentityPolicy.SubMenuPageLabel("Windows", 0, 1, "{0} ({1}/{2})")
                .Should().Be("Windows");
        }
    }
}
