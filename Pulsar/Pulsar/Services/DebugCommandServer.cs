// [Path]: Pulsar/Pulsar/Services/DebugCommandServer.cs

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Native;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Views;

namespace Pulsar.Services
{
    /// <summary>
    /// Debug-mode command pipe. Commands are JSON lines:
    ///   {"command":"menu-open","mode":"action"}   — open the radial menu (Action mode)
    ///   {"command":"menu-open","mode":"task"}     — open the task-switcher menu
    ///   {"command":"menu-close"}                  — dismiss the current menu session
    ///   {"command":"open-settings"}               — open the Settings window (E2E settings-page workflows)
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
                        string? profile = root.TryGetProperty("profile", out var profileEl) && profileEl.ValueKind == JsonValueKind.String
                            ? profileEl.GetString()
                            : null;
                        _logger?.LogInformation("[DebugCommandServer] menu-open mode={Mode} profile={Profile}", mode, profile);
                        var dispatcher = Application.Current?.Dispatcher;
                        if (dispatcher == null)
                        {
                            _logger?.LogWarning("[DebugCommandServer] No WPF application dispatcher; command dropped");
                            return;
                        }
                        _ = dispatcher.InvokeAsync(async () =>
                        {
                            if (!string.IsNullOrWhiteSpace(profile))
                            {
                                _menuViewModel.DebugSetForcedActiveProfile(profile);
                            }
                            await _menuViewModel.ShowMenuForExternalDriverAsync(mode);
                        });
                        break;

                    case "menu-close":
                        _logger?.LogInformation("[DebugCommandServer] menu-close");
                        Application.Current?.Dispatcher?.Invoke(() => _menuViewModel.CancelActiveMenu());
                        break;

                    case "open-settings":
                        _logger?.LogInformation("[DebugCommandServer] open-settings");
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            var app = Application.Current as App;
                            var window = app?.Services?.GetService<SettingsWindow>();
                            if (window == null)
                            {
                                _logger?.LogWarning("[DebugCommandServer] open-settings: SettingsWindow not resolvable from DI");
                                return;
                            }

                            window.Show();
                            window.Activate();
                        });
                        break;

                    // [E2E] Deterministic input synthesis — exercises the same session
                    // entry points as the real adapters without registering global
                    // input hooks. slot-click/hover resolve the slot's current centre
                    // from its live layout (cascade children included); selection-execute
                    // runs the hotkey/gesture release path.
                    case "slot-click":
                    {
                        int slot = TryGetInt(root, "slot", 0);
                        string button = TryGetString(root, "button") ?? "left";
                        _logger?.LogInformation("[DebugCommandServer] slot-click slot={Slot} button={Button}", slot, button);
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            ParkCursorOnSlot(slot);
                            _menuViewModel.SimulateSlotClick(slot, button);
                        });
                        break;
                    }

                    case "slot-hover":
                    {
                        int slot = TryGetInt(root, "slot", 0);
                        _logger?.LogInformation("[DebugCommandServer] slot-hover slot={Slot}", slot);
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            ParkCursorOnSlot(slot);
                            _menuViewModel.SimulateSlotHover(slot);
                        });
                        break;
                    }

                    case "selection-execute":
                        _logger?.LogInformation("[DebugCommandServer] selection-execute");
                        Application.Current?.Dispatcher?.Invoke(_menuViewModel.SimulateSelectionExecute);
                        break;

                    case "selection-execute-dismiss":
                        _logger?.LogInformation("[DebugCommandServer] selection-execute-dismiss");
                        Application.Current?.Dispatcher?.Invoke(_menuViewModel.SimulateSelectionExecuteAndDismiss);
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

        private static int TryGetInt(JsonElement root, string name, int fallback)
        {
            return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number
                ? el.GetInt32()
                : fallback;
        }

        /// <summary>
        /// Moves the physical pointer onto the slot's current centre before a
        /// synthetic click/hover. The rendering sampler feeds the real cursor
        /// position back into hover tracking, so without this the next sampler
        /// tick overwrites the synthetic hover with the stale pointer location.
        /// </summary>
        private void ParkCursorOnSlot(int slot)
        {
            try
            {
                var screenPoint = _menuViewModel.GetSlotScreenPoint(slot);
                PulsarNative.SetCursorPos((int)screenPoint.X, (int)screenPoint.Y);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DebugCommandServer] Failed to park cursor on slot {Slot}", slot);
            }
        }

        private static string? TryGetString(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }
    }
}
