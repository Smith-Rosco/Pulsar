using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// 内置书签脚本示例库：以代码注册精选示例（id + 本地化标题/描述），
    /// 脚本内容从随应用分发的 .js 资源中读取。镜像 TutorialScenarioRegistry 模式。
    /// 导入（<see cref="ImportAsync"/>）通过 <see cref="ScriptFileService"/> 将示例
    /// 内容复制到用户脚本目录，内置资源保持只读。
    /// </summary>
    public sealed class ExampleLibraryService
    {
        private readonly ILocalizationService? _loc;
        private readonly ILogger<ExampleLibraryService>? _logger;
        private readonly string _assetRoot;
        private readonly IScriptFileService? _fileService;
        private readonly IReadOnlyList<ExampleRegistration> _registrations;

        private sealed record ExampleRegistration(
            string Id,
            string TitleKey,
            string DescriptionKey,
            string AssetFileName);

        public ExampleLibraryService(
            ILocalizationService? localizationService = null,
            string? assetRoot = null,
            IScriptFileService? fileService = null,
            ILogger<ExampleLibraryService>? logger = null)
        {
            _loc = localizationService;
            _logger = logger;
            _fileService = fileService;
            _assetRoot = assetRoot ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Scripts", "Demo");
            _registrations = BuildRegistrations();
        }

        private static IReadOnlyList<ExampleRegistration> BuildRegistrations()
        {
            return new List<ExampleRegistration>
            {
                new("hello", "Example.Hello.Title", "Example.Hello.Description", "browser_demo.js"),
                new("form-fill", "Example.FormFill.Title", "Example.FormFill.Description", "form_fill_demo.js"),
                new("data-extract", "Example.DataExtract.Title", "Example.DataExtract.Description", "data_extract_demo.js"),
                new("link-traverse", "Example.LinkTraverse.Title", "Example.LinkTraverse.Description", "link_traverse_demo.js")
            };
        }

        public IReadOnlyList<ExampleLibraryItem> GetAll()
        {
            return _registrations.Select(BuildItem).ToList();
        }

        public ExampleLibraryItem? GetById(string id)
        {
            var registration = _registrations.FirstOrDefault(r =>
                string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            return registration is null ? null : BuildItem(registration);
        }

        /// <summary>
        /// Copies an example's content into the user's scripts directory via
        /// <see cref="IScriptFileService"/>, under a distinct file name derived
        /// from the example id (suffixed on collision). The built-in asset is
        /// never overwritten. Returns the full path of the saved copy, or null
        /// when the example id is unknown.
        /// </summary>
        public async Task<string?> ImportAsync(string exampleId, string? suggestedName = null)
        {
            if (_fileService is null)
            {
                throw new InvalidOperationException("ExampleLibraryService is not configured with a ScriptFileService.");
            }

            var item = GetById(exampleId);
            if (item is null)
            {
                return null;
            }

            var fileName = !string.IsNullOrWhiteSpace(suggestedName)
                ? suggestedName
                : BuildImportFileName(item.Id);

            return await _fileService.SaveScriptAsync(item.Content, fileName);
        }

        private static string BuildImportFileName(string exampleId)
        {
            var stem = new string(exampleId
                .Where(char.IsLetterOrDigit)
                .Select(c => c)
                .ToArray());
            return string.IsNullOrWhiteSpace(stem) ? "example" : stem;
        }

        private ExampleLibraryItem BuildItem(ExampleRegistration registration)
        {
            return new ExampleLibraryItem
            {
                Id = registration.Id,
                Title = _loc?[registration.TitleKey] ?? registration.TitleKey,
                Description = _loc?[registration.DescriptionKey] ?? registration.DescriptionKey,
                Content = ReadAsset(registration.AssetFileName)
            };
        }

        private string ReadAsset(string fileName)
        {
            try
            {
                var path = Path.Combine(_assetRoot, fileName);
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[ExampleLibrary] Failed to read asset {FileName}", fileName);
                return string.Empty;
            }
        }
    }

    public sealed class ExampleLibraryItem
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public required string Description { get; init; }

        public required string Content { get; init; }
    }
}
