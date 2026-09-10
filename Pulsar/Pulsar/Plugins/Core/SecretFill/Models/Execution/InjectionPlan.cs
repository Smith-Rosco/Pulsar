using System;
using System.Collections.Generic;

namespace Pulsar.Plugins.Core.SecretFill.Models.Execution
{
    public sealed class InjectionPlan
    {
        public InjectionPlan(Guid secretId, IReadOnlyList<InjectionStep> steps)
        {
            SecretId = secretId;
            Steps = steps;
        }

        public Guid SecretId { get; }

        public IReadOnlyList<InjectionStep> Steps { get; }
    }
}
