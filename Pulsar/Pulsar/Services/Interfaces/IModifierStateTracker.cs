namespace Pulsar.Services.Interfaces
{
    public interface IModifierStateTracker
    {
        void OnSyntheticEventBegin();
        void OnSyntheticEventEnd();
        void ResetAllModifiers();
    }
}
