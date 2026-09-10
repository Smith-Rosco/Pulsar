using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models.Execution;

namespace Pulsar.Plugins.Core.SecretFill.Services
{
    public class SecretFillExecutionService : ISecretFillExecutionService
    {
        private readonly ISecretStore _secretStore;
        private readonly ISecretProtector _secretProtector;
        private readonly IInjectionExecutor _injectionExecutor;
        private readonly ILogger<SecretFillExecutionService> _logger;

        public SecretFillExecutionService(
            ISecretStore secretStore,
            ISecretProtector secretProtector,
            IInjectionExecutor injectionExecutor,
            ILogger<SecretFillExecutionService> logger)
        {
            _secretStore = secretStore;
            _secretProtector = secretProtector;
            _injectionExecutor = injectionExecutor;
            _logger = logger;
        }

        public async Task<SecretFillExecutionResult> ExecuteAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!InjectionRequest.TryCreate(args, context, out var request, out var validationMessage)
                || request == null)
            {
                return SecretFillExecutionResult.Fail(SecretFillExecutionStage.Validation, validationMessage);
            }

            _logger.LogInformation("[SecretFillExecutionService] Starting execution for secret {SecretId}", request.SecretId);

            var secrets = await _secretStore.LoadAsync();
            if (!secrets.TryGetValue(request.SecretId, out var payload))
            {
                return SecretFillExecutionResult.Fail(SecretFillExecutionStage.SecretLookup, $"Secret not found: {request.SecretId}");
            }

            if (string.IsNullOrWhiteSpace(payload.EncryptedData))
            {
                return SecretFillExecutionResult.Fail(SecretFillExecutionStage.Decryption, "Secret data is empty");
            }

            string password = _secretProtector.Decrypt(payload.EncryptedData);
            if (string.IsNullOrEmpty(password))
            {
                return SecretFillExecutionResult.Fail(SecretFillExecutionStage.Decryption, "Decryption failed");
            }

            var plan = BuildPlan(request, payload.Account, password);
            return await _injectionExecutor.ExecuteAsync(plan);
        }

        private static InjectionPlan BuildPlan(InjectionRequest request, string? account, string password)
        {
            var steps = new List<InjectionStep>
            {
                new(InjectionStepType.HideLauncher),
                new(InjectionStepType.RestoreFocus, null, 0, request.TargetWindowHandle),
                new(InjectionStepType.Delay, null, 100)
            };

            int delay = request.InjectionDelay;

            if (!string.IsNullOrWhiteSpace(account))
            {
                steps.Add(new InjectionStep(InjectionStepType.SendText, account));
                if (delay > 0) steps.Add(new InjectionStep(InjectionStepType.Delay, null, delay));
                steps.Add(new InjectionStep(InjectionStepType.SendKey, "{TAB}"));
                if (delay > 0) steps.Add(new InjectionStep(InjectionStepType.Delay, null, delay));
            }

            steps.Add(new InjectionStep(InjectionStepType.SendText, password));

            if (request.AutoEnter)
            {
                if (delay > 0) steps.Add(new InjectionStep(InjectionStepType.Delay, null, delay));
                steps.Add(new InjectionStep(InjectionStepType.SendKey, "{ENTER}"));
            }

            return new InjectionPlan(request.SecretId, steps);
        }
    }
}
