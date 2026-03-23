using System;
using System.Threading.Tasks;

namespace Pulsar.ViewModels.Base
{
    public interface IDialogViewModel
    {
        Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result);
        Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }
    }
}
