using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Features.Presets.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Presets
{
    public class PresetServiceRegistrationTests
    {
        [Fact]
        public void PresetServices_ResolveFromContainer()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IConfigService>());
            services.AddSingleton<IPluginPermissionService, PluginPermissionService>();
            services.AddSingleton(Mock.Of<ILocalizationService>());
            services.AddSingleton<IPresetCatalogService, PresetCatalogService>();
            services.AddSingleton<IPresetInstallService, PresetInstallService>();

            using var provider = services.BuildServiceProvider();

            var catalog = provider.GetRequiredService<IPresetCatalogService>();
            var install = provider.GetRequiredService<IPresetInstallService>();

            catalog.Should().NotBeNull();
            install.Should().NotBeNull();
            catalog.All.Should().NotBeEmpty();
        }
    }
}
