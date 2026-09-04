// [Path]: Pulsar/Pulsar/Services/DebugCommandServer.cs

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Services
{
    /// <summary>
    /// Debug-mode command pipe. Commands are JSON lines:
    ///   {"command":"menu-open","mode":"action"}   — open the radial menu (Action mode)
    ///   {"command":"menu-open","mode":"task"}     — open the task-switcher menu
    ///   {"command":"menu-close"}                  — dismiss the current menu session
    ///
    /// This is the spec-mandated explicit trigger channel for debug mode, where no
    /// global input hooks are registered by default. All command handlers are
    /// marshalled onto the WPF dispatcher; a malformed command only logs a warning.
    /// </summary>
    public sealed class DebugCommandServer : IDebugCommandServer
    {
        private readonly ILogger<DebugCommandServer>? _logger;
        private readonly RadialMenuViewModel _menuViewModel;
        private CancellationTokenSource? _cts;
        private Task? _serverLoop;

        public DebugCommandServer(RadialMenuViewModel menuViewModel, ILogger<DebugCommandServer>? logger = null)
        {
            _menuViewModel = menuViewModel;
            _logger = logger;
        }

        public void Start(string pipeName)
        {
            lock (this)
            {
                if (_serverLoop != null)
                {
                    return;
                }

                _cts = new CancellationTokenSource();
                _serverLoop = Task.Run(() => ServerLoopAsync(pipeName, _cts.Token));
                _logger?.LogInformation("[DebugCommandServer] Listening on {PipeName}", pipeName);
            }
        }

        public void Stop()
        {
            lock (this)
            {
                _cts?.Cancel();
                _serverLoop = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task ServerLoopAsync(string pipeName, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
                    server = null;

                    while (!token.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                        if (line == null)
                        {
                            break; // client disconnected; accept the next one
                        }

                        HandleCommand(line);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[DebugCommandServer] Server loop error; retrying in 1s");
                    try { await Task.Delay(1000, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
                finally
                {
                    try { server?.Dispose(); } catch { /* best effort */ }
                }
            }
        }

        private void HandleCommand(string line)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var command = root.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() : null;

                switch (command?.ToLowerInvariant())
                {
                    case "menu-open":
                        var mode = root.TryGetProperty("mode", out var modeEl) && modeEl.ValueKind == JsonValueKind.String
                            && modeEl.GetString()?.Equals("task", StringComparison.OrdinalIgnoreCase) == true
                                ? RadialMenuMode.Task
                                : RadialMenuMode.Action;
                        _logger?.LogInformation("[DebugCommandServer] menu-open mode={Mode}", mode);
                        var dispatcher = Application.Current?.Dispatcher;
                        if (dispatcher == null)
                        {
                            _logger?.LogWarning("[DebugCommandServer] No WPF application dispatcher; command dropped");
                            return;
                        }
                        _ = dispatcher.InvokeAsync(async () => await _menuViewModel.ShowMenuForExternalDriverAsync(mode));
                        break;

                    case "menu-close":
                        _logger?.LogInformation("[DebugCommandServer] menu-close");
                        Application.Current?.Dispatcher?.Invoke(() => _menuViewModel.CancelActiveMenu());
                        break;

                    default:
                        _logger?.LogWarning("[DebugCommandServer] Unknown command: {Line}", line);
                        break;
                }
            }
            catch (JsonException)
            {
                _logger?.LogWarning("[DebugCommandServer] Malformed command line: {Line}", line);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DebugCommandServer] Command execution failed: {Line}", line);
            }
        }
    }
}
