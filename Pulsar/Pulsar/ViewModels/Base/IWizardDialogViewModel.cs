using System.ComponentModel;
using System.Windows.Input;

namespace Pulsar.ViewModels.Base
{
    /// <summary>
    /// Implemented by wizard-style dialog ViewModels that need to drive
    /// the DialogHostWindow footer buttons dynamically across steps.
    /// The DialogHostViewModel watches this interface when Content is set
    /// and reflects PrimaryButtonText, SecondaryButtonText and their
    /// visibility from here instead of the static ConfigureButtons path.
    /// </summary>
    public interface IWizardDialogViewModel : IDialogViewModel, INotifyPropertyChanged
    {
        /// <summary>Label for the primary (right) button. e.g. "Next" or "Confirm".</summary>
        string PrimaryButtonText { get; }

        /// <summary>Label for the secondary (left) button. e.g. "Back" or "Cancel".</summary>
        string SecondaryButtonText { get; }

        /// <summary>Whether the primary button should be shown.</summary>
        bool IsPrimaryButtonVisible { get; }

        /// <summary>Whether the secondary button should be shown.</summary>
        bool IsSecondaryButtonVisible { get; }

        /// <summary>
        /// Command invoked when the primary footer button is clicked.
        /// </summary>
        ICommand PrimaryCommand { get; }

        /// <summary>
        /// Command invoked when the secondary footer button is clicked.
        /// </summary>
        ICommand SecondaryCommand { get; }
    }
}
