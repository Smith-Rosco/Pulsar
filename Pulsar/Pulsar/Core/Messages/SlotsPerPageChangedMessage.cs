namespace Pulsar.Core.Messages
{
    /// <summary>
    /// Message sent when the user changes the slots per page configuration in Settings.
    /// This triggers immediate layout recalculation in RadialMenuViewModel.
    /// </summary>
    public class SlotsPerPageChangedMessage
    {
        public int NewCount { get; }
        
        public SlotsPerPageChangedMessage(int newCount)
        {
            NewCount = newCount;
        }
    }
}
