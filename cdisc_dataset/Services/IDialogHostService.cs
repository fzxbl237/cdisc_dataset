using System.Threading.Tasks;
using Prism.Dialogs;

namespace cdisc_dataset.Services;

public interface IDialogHostService: IDialogService
{
    Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters parameters, string dialogHostName = "Root");
}
