using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core;
using Pulsar.Core.Focus;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Plugins.Core.Pki.Services.Input;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Serilog;
using Serilog.Formatting.Json;

namespace Pulsar.Simulator
{
    class Program
    {
        public class Options
        {
            [Option('p', "plugin", Required = true, HelpText = "The ID of the plugin to execute (e.g., com.pulsar.winswitcher)")]
            public string PluginId { get; set; } = string.Empty;

            [Option('a', "action", Required = false, Default = "run", HelpText = "The action to execute")]
            public string Action { get; set; } = "run";

            [Option('g', "args", Required = false, HelpText = "JSON string of arguments for the plugin")]
            public string ArgsJson { get; set; } = "{}";

            [Option('c', "context", Required = false, HelpText = "Path to a JSON file containing mock process info")]
            public string ContextFile { get; set; } = string.Empty;
        }

        static async Task<int> Main(string[] args)
        {
            // Configure Serilog to output JSON logs to stdout for AI to parse
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(new JsonFormatter(renderMessage: true))
                .CreateLogger();

            try
            {
                return await Parser.Default.ParseArguments<Options>(args)
                    .MapResult(
                        (Options opts) => RunExecutionAsync(opts),
                        errs => Task.FromResult(1));
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        static async Task<int> RunExecutionAsync(Options opts)
        {
            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger);
            });

            // Mock core dependencies
            var mockConfig = new Mock<IConfigService>();
            mockConfig.Setup(c => c.Current).Returns(new ProfilesConfig());
            services.AddSingleton(mockConfig.Object);

            var mockWindow = new Mock<IWindowService>();
            mockWindow.Setup(w => w.GetPreviousWindow()).Returns(IntPtr.Zero);
            mockWindow.Setup(w => w.GetProcessWindowsAsync(It.IsAny<int>())).ReturnsAsync(new List<ProcessWindowInfo>());
            mockWindow.Setup(w => w.HideMainWindow());
            
            services.AddSingleton(mockWindow.Object);

            var mockFocusManager = new Mock<IFocusManager>();
            mockFocusManager.Setup(f => f.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });
            services.AddSingleton(mockFocusManager.Object);

            services.AddSingleton<ISecretProtector, CredentialsManager>();
            services.AddSingleton<IPkiSecretStore, SecretRepository>();
            services.AddSingleton<IPkiSecretMetadataResolver, PkiSecretMetadataResolver>();
            services.AddSingleton<IInjectionExecutor, SendKeysInjectionExecutor>();
            services.AddSingleton<IPkiExecutionService, PkiExecutionService>();
            services.AddSingleton<ISendKeysWriter, WindowsSendKeysWriter>();

            // Plugin runtime
            var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            services.AddPluginRuntime(pluginDir);

            var serviceProvider = services.BuildServiceProvider();

            // Initialize PluginRegistry
            var registry = serviceProvider.GetRequiredService<IPluginRegistry>();
            await registry.LoadCoreAsync();

            // Prepare Parameters
            var pluginArgs = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(opts.ArgsJson))
            {
                try
                {
                    pluginArgs = JsonSerializer.Deserialize<Dictionary<string, string>>(opts.ArgsJson) ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to parse args JSON");
                    return 1;
                }
            }

            // Create Context
            var windowService = serviceProvider.GetRequiredService<IWindowService>();
            var context = PulsarContext.Capture(windowService);

            Log.Information("Executing plugin {PluginId} with action {Action}", opts.PluginId, opts.Action);
            var result = await registry.ExecuteAsync(opts.PluginId, opts.Action, pluginArgs, context);

            // Output Result as JSON
            var resultJson = JsonSerializer.Serialize(new
            {
                Success = result.Success,
                Message = result.Message,
                Cue = result.Cue.ToString(),
                Severity = result.Severity.ToString()
            }, new JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine("\n--- SIMULATION RESULT ---");
            Console.WriteLine(resultJson);
            Console.WriteLine("-------------------------\n");

            return result.Success ? 0 : 1;
        }
    }
}
