using System;
using System.Collections.Generic;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.Core.Pki.Models.Execution
{
    public sealed class InjectionRequest
    {
        private InjectionRequest(Guid secretId, bool autoEnter, IntPtr targetWindowHandle)
        {
            SecretId = secretId;
            AutoEnter = autoEnter;
            TargetWindowHandle = targetWindowHandle;
        }

        public Guid SecretId { get; }

        public bool AutoEnter { get; }

        public IntPtr TargetWindowHandle { get; }

        public static bool TryCreate(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            out InjectionRequest? request,
            out string errorMessage)
        {
            request = null;

            if (!args.TryGetValue("secretId", out var rawSecretId) || string.IsNullOrWhiteSpace(rawSecretId))
            {
                errorMessage = "Missing required parameter: secretId";
                return false;
            }

            if (!Guid.TryParse(rawSecretId, out var secretId))
            {
                errorMessage = $"Invalid secret ID format: {rawSecretId}";
                return false;
            }

            bool autoEnter = TryReadBool(args, "autoEnter") || TryReadBool(args, "autoSubmit");

            request = new InjectionRequest(secretId, autoEnter, context.TargetWindowHandle);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryReadBool(IReadOnlyDictionary<string, string> args, string key)
        {
            return args.TryGetValue(key, out var rawValue)
                && bool.TryParse(rawValue, out var value)
                && value;
        }
    }
}
