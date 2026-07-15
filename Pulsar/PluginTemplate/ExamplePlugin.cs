using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;

namespace ExamplePlugin;

public class ExamplePlugin : IPulsarPlugin
{
    private ILogger<ExamplePlugin>? _logger;

    public string Id => "com.example.exampleplugin";
    public string DisplayName => "Example Plugin";
    public string Description => "Demonstrates the Pulsar plugin system";
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Icon => "🔌";
    public bool CanDisable => true;

    public void Initialize(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetService(typeof(ILogger<ExamplePlugin>)) as ILogger<ExamplePlugin>;
        _logger?.LogInformation("[{PluginName}] Initialized", DisplayName);
    }

    public async Task<PluginResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string> args,
        PulsarContext context)
    {
        try
        {
            var windowTitle = context.ActiveWindow?.Title ?? "Unknown";
            var name = args.TryGetValue("name", out var n) ? n : "World";
            return PluginResult.Ok($"Hello {name}!\nCurrent window: {windowTitle}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[{PluginName}] Execute failed", DisplayName);
            return PluginResult.Error(ex.Message);
        }
    }

    public void Dispose()
    {
        _logger?.LogInformation("[{PluginName}] Disposed", DisplayName);
    }
}
