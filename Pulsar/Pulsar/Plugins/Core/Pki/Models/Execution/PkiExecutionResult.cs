namespace Pulsar.Plugins.Core.Pki.Models.Execution
{
    public sealed class PkiExecutionResult
    {
        private PkiExecutionResult(bool success, PkiExecutionStage stage, string message, InjectionPlan? plan)
        {
            Success = success;
            Stage = stage;
            Message = message;
            Plan = plan;
        }

        public bool Success { get; }

        public PkiExecutionStage Stage { get; }

        public string Message { get; }

        public InjectionPlan? Plan { get; }

        public static PkiExecutionResult Ok(string message, InjectionPlan plan)
        {
            return new PkiExecutionResult(true, PkiExecutionStage.Completed, message, plan);
        }

        public static PkiExecutionResult Fail(PkiExecutionStage stage, string message, InjectionPlan? plan = null)
        {
            return new PkiExecutionResult(false, stage, message, plan);
        }
    }
}
