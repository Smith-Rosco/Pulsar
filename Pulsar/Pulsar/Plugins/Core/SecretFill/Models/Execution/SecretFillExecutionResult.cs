namespace Pulsar.Plugins.Core.SecretFill.Models.Execution
{
    public sealed class SecretFillExecutionResult
    {
        private SecretFillExecutionResult(bool success, SecretFillExecutionStage stage, string message, InjectionPlan? plan)
        {
            Success = success;
            Stage = stage;
            Message = message;
            Plan = plan;
        }

        public bool Success { get; }

        public SecretFillExecutionStage Stage { get; }

        public string Message { get; }

        public InjectionPlan? Plan { get; }

        public static SecretFillExecutionResult Ok(string message, InjectionPlan plan)
        {
            return new SecretFillExecutionResult(true, SecretFillExecutionStage.Completed, message, plan);
        }

        public static SecretFillExecutionResult Fail(SecretFillExecutionStage stage, string message, InjectionPlan? plan = null)
        {
            return new SecretFillExecutionResult(false, stage, message, plan);
        }
    }
}
