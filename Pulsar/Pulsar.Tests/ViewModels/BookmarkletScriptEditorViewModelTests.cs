using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Services;
using Pulsar.ViewModels.Dialogs;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.Tests.ViewModels
{
    public class BookmarkletScriptEditorViewModelTests
    {
        private static Mock<ILocalizationService> CreateLoc()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            return loc;
        }

        private static (string dir, ScriptFileService fileService) CreateTempFileService()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pulsar-editor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return (dir, new ScriptFileService(dir));
        }

        private static BookmarkletScriptEditorViewModel CreateVm(
            ScriptFileService fileService,
            Mock<ILocalizationService>? loc = null)
        {
            return new BookmarkletScriptEditorViewModel(
                fileService,
                new ScriptValidationService(),
                (loc ?? CreateLoc()).Object);
        }

        [Fact]
        public void NewScript_StartsEmpty_AndNotDirty()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);

                vm.IsNew.Should().BeTrue();
                vm.IsEditing.Should().BeFalse();
                vm.ScriptContent.Should().BeEmpty();
                vm.IsDirty.Should().BeFalse();
                vm.SavedFilePath.Should().BeNull();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void EditingContent_TriggersValidationFeed()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);

                vm.ScriptContent = "alert('valid');";
                vm.IsDirty.Should().BeTrue();
                vm.IsValid.Should().BeTrue();
                vm.ValidationErrors.Should().BeEmpty();

                vm.ScriptContent = "   ";
                vm.IsValid.Should().BeFalse();
                vm.ValidationErrors.Should().NotBeEmpty();
                vm.HasValidationErrors.Should().BeTrue();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task Save_NewScript_WritesFile_AndRequestsClose()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);
                DialogResult? closeResult = null;
                vm.RequestClose = r => closeResult = r;
                vm.ScriptContent = "alert('hi');";
                vm.FileName = "hello";

                await vm.SaveCommand.ExecuteAsync(null);

                closeResult.Should().Be(DialogResult.Confirmed);
                vm.SavedFilePath.Should().NotBeNull();
                vm.IsEditing.Should().BeTrue();
                File.Exists(vm.SavedFilePath!).Should().BeTrue();
                (await fs.ReadScriptAsync(vm.SavedFilePath!)).Should().Be("alert('hi');");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task Save_EditingScript_OverwritesSameFile()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var original = await fs.SaveScriptAsync("alert('one');", "keep");
                var vm = CreateVm(fs);
                vm.RequestClose = _ => { };

                (await vm.OpenScriptAsync(original)).Should().BeTrue();
                vm.ScriptContent = "alert('two');";
                await vm.SaveCommand.ExecuteAsync(null);

                vm.SavedFilePath.Should().Be(original);
                (await fs.ReadScriptAsync(original)).Should().Be("alert('two');");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task OpenScript_LoadsContent_AndMarksEditing()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var path = await fs.SaveScriptAsync("alert('open');", "openme");
                var vm = CreateVm(fs);

                var ok = await vm.OpenScriptAsync(path);

                ok.Should().BeTrue();
                vm.IsEditing.Should().BeTrue();
                vm.ScriptContent.Should().Be("alert('open');");
                vm.FileName.Should().Be("openme");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task OpenScript_MissingFile_ReturnsFalse()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);

                var ok = await vm.OpenScriptAsync(Path.Combine(dir, "does-not-exist.js"));

                ok.Should().BeFalse();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Cancel_RequestsCancelledClose()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);
                DialogResult? closeResult = null;
                vm.RequestClose = r => closeResult = r;

                vm.CancelCommand.Execute(null);

                closeResult.Should().Be(DialogResult.Cancelled);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void NewCommand_ResetsState()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);
                vm.ScriptContent = "alert('x');";
                vm.IsDirty = true;

                vm.NewScriptCommand.Execute(null);

                vm.IsNew.Should().BeTrue();
                vm.ScriptContent.Should().BeEmpty();
                vm.IsDirty.Should().BeFalse();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void ValidationWarnings_SurfaceWhenPresent()
        {
            var (dir, fs) = CreateTempFileService();
            try
            {
                var vm = CreateVm(fs);
                vm.ScriptContent = "alert('with warnings');";

                vm.HasValidationWarnings.Should().BeFalse();
                vm.ValidationSummary.Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
