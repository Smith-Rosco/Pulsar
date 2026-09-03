using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;

namespace Pulsar.Services
{
    /// <summary>
    /// 内置书签脚本示例库：以代码注册精选示例（id + 本地化标题/描述），
    /// 脚本内容从随应用分发的 .js 资源中读取。镜像 TutorialScenarioRegistry 模式。
    /// </summary>
    public sealed class ExampleLibraryService
    {
        private readonly ILocalizationService? _loc;
        private readonly ILogger<ExampleLibraryService>? _logger;
        private readonly string _assetRoot;
        private readonly IReadOnlyList<ExampleRegistration> _registrations;

        private sealed record ExampleRegistration(
            string Id,
            string TitleKey,
            string DescriptionKey,
            string AssetFileName);

        public ExampleLibraryService(
            ILocalizationService? localizationService = null,
            string? assetRoot = null,
            ILogger<ExampleLibraryService>? logger = null)
        {
            _loc = localizationService;
            _logger = logger;
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
