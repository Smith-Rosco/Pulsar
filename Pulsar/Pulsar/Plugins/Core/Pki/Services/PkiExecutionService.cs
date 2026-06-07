using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models.Execution;

namespace Pulsar.Plugins.Core.Pki.Services
{
    public class PkiExecutionService : IPkiExecutionService
    {
        private readonly IPkiSecretStore _secretStore;
        private readonly ISecretProtector _secretProtector;
        private readonly IInjectionExecutor _injectionExecutor;
        private readonly ILogger<PkiExecutionService> _logger;

        public PkiExecutionService(
            IPkiSecretStore secretStore,
            ISecretProtector secretProtector,
            IInjectionExecutor injectionExecutor,
            ILogger<PkiExecutionService> logger)
        {
            _secretStore = secretStore;
            _secretProtector = secretProtector;
            _injectionExecutor = injectionExecutor;
            _logger = logger;
        }

        public async Task<PkiExecutionResult> ExecuteAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!InjectionRequest.TryCreate(args, context, out var request, out var validationMessage)
                || request == null)
            {
                return PkiExecutionResult.Fail(PkiExecutionStage.Validation, validationMessage);
            }

            _logger.LogInformation("[PkiExecutionService] Starting execution for secret {SecretId}", request.SecretId);

            var secrets = await _secretStore.LoadAsync();
            if (!secrets.TryGetValue(request.SecretId, out var payload))
            {
                return PkiExecutionResult.Fail(PkiExecutionStage.SecretLookup, $"Secret not found: {request.SecretId}");
            }

            if (string.IsNullOrWhiteSpace(payload.EncryptedData))
            {
                return PkiExecutionResult.Fail(PkiExecutionStage.Decryption, "Secret data is empty");
            }

            string password = _secretProtector.Decrypt(payload.EncryptedData);
            if (string.IsNullOrEmpty(password))
            {
                return PkiExecutionResult.Fail(PkiExecutionStage.Decryption, "Decryption failed");
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
