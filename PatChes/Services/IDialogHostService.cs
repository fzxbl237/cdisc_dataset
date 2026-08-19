using System.Threading.Tasks;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.Services;

public interface IDialogHostService
{
    Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters, string dialogHostName = "Root");
}
