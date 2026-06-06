using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Focus;
using Pulsar.Native;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Plugins.Core.Pki.Services.Input;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Core.Pki.Services
{
    public class SendKeysInjectionExecutor : IInjectionExecutor
    {
        private readonly IWindowService _windowService;
        private readonly IFocusManager _focusManager;
        private readonly ISendKeysWriter _sendKeysWriter;
        private readonly ILogger<SendKeysInjectionExecutor> _logger;

        public SendKeysInjectionExecutor(
            IWindowService windowService,
            IFocusManager focusManager,
            ISendKeysWriter sendKeysWriter,
            ILogger<SendKeysInjectionExecutor> logger)
        {
            _windowService = windowService;
            _focusManager = focusManager;
            _sendKeysWriter = sendKeysWriter;
            _logger = logger;
        }

        public async Task<PkiExecutionResult> ExecuteAsync(InjectionPlan plan)
        {
            try
            {
                foreach (var step in plan.Steps)
                {
                    switch (step.Type)
                    {
                        case InjectionStepType.HideLauncher:
                            try
                            {
                                _windowService.HideMainWindow();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[SendKeysInjectionExecutor] Failed to hide launcher");
                                return PkiExecutionResult.Fail(PkiExecutionStage.HideLauncher, "Failed to hide launcher", plan);
                            }
                            break;

                        case InjectionStepType.RestoreFocus:
                            try
                            {
                                var options = new FocusActivationOptions
                                {
                                    VerifyAfterActivation = true,
                                    FlashAfterActivation = false
                                };
                                var result = await _focusManager.ActivateWindowAsync(step.TargetWindowHandle, options);
                                if (!result.Success || !result.VerificationPassed)
                                {
                                    _logger.LogError("[SendKeysInjectionExecutor] Focus restore/verification failed for 0x{hWnd:X}", step.TargetWindowHandle.ToInt64());
                                    return PkiExecutionResult.Fail(PkiExecutionStage.FocusRestore, "Failed to restore focus to target window", plan);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[SendKeysInjectionExecutor] Failed to restore focus");
                                return PkiExecutionResult.Fail(PkiExecutionStage.FocusRestore, "Failed to restore focus to target window", plan);
                            }
                            break;

                        case InjectionStepType.Delay:
                            await Task.Delay(step.DelayMilliseconds);
                            break;

                        case InjectionStepType.SendText:
                            try
                            {
                                _sendKeysWriter.SendWait(step.Value ?? string.Empty);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[SendKeysInjectionExecutor] Failed to send text");
                                return PkiExecutionResult.Fail(PkiExecutionStage.Injection, "Credential injection failed during text entry", plan);
                            }
                            break;

                        case InjectionStepType.SendKey:
                            try
                            {
                                ExecuteSendKey(step.Value ?? string.Empty);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[SendKeysInjectionExecutor] Failed to send key");
                                return PkiExecutionResult.Fail(PkiExecutionStage.Injection, "Credential injection failed during key entry", plan);
                            }
                            break;
                    }
                }

                _logger.LogInformation("[SendKeysInjectionExecutor] Injection sequence finished for secret {SecretId}", plan.SecretId);
                return PkiExecutionResult.Ok("Credentials injected successfully", plan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SendKeysInjectionExecutor] Unexpected injection failure");
                return PkiExecutionResult.Fail(PkiExecutionStage.Injection, "Credential injection failed", plan);
            }
        }

        private static void ExecuteSendKey(string value)
        {
            if (value.Length >= 2 && value[0] == '{' && value[^1] == '}')
            {
                string token = value[1..^1];
                if (InputHelper.GetNamedKey(token) is ushort vk)
                {
                    InputHelper.SendKeyCombination(vk);
                    return;
                }
            }

            InputHelper.SendText(value);
        }
    }
}
