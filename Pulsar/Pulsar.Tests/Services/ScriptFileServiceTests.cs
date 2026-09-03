using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Services;

namespace Pulsar.Tests.Services
{
    public class ScriptFileServiceTests
    {
        private static string CreateTempScriptsDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pulsar-scripts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public async Task SaveScript_ThenReadScript_RoundTripsContent()
        {
            var dir = CreateTempScriptsDir();
            try
            {
                var service = new ScriptFileService(dir);
                var path = await service.SaveScriptAsync("alert('hi');", "hello");

                path.Should().StartWith(dir);
                path.Should().EndWith(".js");
                File.Exists(path).Should().BeTrue();
                (await service.ReadScriptAsync(path)).Should().Be("alert('hi');");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task SaveScript_AddsJsExtension_WhenSuggestedNameLacksIt()
        {
            var dir = CreateTempScriptsDir();
            try
            {
                var service = new ScriptFileService(dir);
                var path = await service.SaveScriptAsync("x", "myScript");

                Path.GetExtension(path).Should().Be(".js");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task SaveScript_UsesUniqueNames_WhenFileExists()
        {
            var dir = CreateTempScriptsDir();
            try
            {
                var service = new ScriptFileService(dir);
                var first = await service.SaveScriptAsync("one", "same");
                var second = await service.SaveScriptAsync("two", "same");
                var third = await service.SaveScriptAsync("three", "same");

                first.Should().NotBe(second);
                second.Should().NotBe(third);
                var all = await service.ListScriptsAsync();
                all.Count.Should().Be(3);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public async Task SaveScript_CreatesDirectoryOnDemand()
        {
            var parent = Path.Combine(Path.GetTempPath(), "pulsar-parent-" + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(parent, "nested", "Scripts");
            try
            {
                var service = new ScriptFileService(dir);
                Directory.Exists(dir).Should().BeFalse();

                var path = await service.SaveScriptAsync("content", "auto");
                Directory.Exists(dir).Should().BeTrue();
                File.Exists(path).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(parent))
                {
                    Directory.Delete(parent, recursive: true);
                }
            }
        }

        [Fact]
        public async Task ListScripts_ReturnsOnlyJsFiles()
        {
            var dir = CreateTempScriptsDir();
            try
            {
                var service = new ScriptFileService(dir);
                await service.SaveScriptAsync("a", "a.js");
                await service.SaveScriptAsync("b", "b.js");
                await File.WriteAllTextAsync(Path.Combine(dir, "notes.txt"), "not a script");

                var scripts = await service.ListScriptsAsync();
                scripts.Count.Should().Be(2);
                scripts.Should().OnlyContain(p => p.EndsWith(".js", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void IsPathInsideScripts_ReturnsFalseForOutsidePath()
        {
            var dir = CreateTempScriptsDir();
            try
            {
                var service = new ScriptFileService(dir);
                service.IsPathInsideScripts(Path.Combine(dir, "a.js")).Should().BeTrue();
                service.IsPathInsideScripts(@"C:\Windows\Temp\evil.js").Should().BeFalse();
                service.IsPathInsideScripts(string.Empty).Should().BeFalse();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
