using System.Threading;
using System.Threading.Tasks;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.Services;

public interface IDialogHostAware : IDialogAware
{
    string? DialogHostName { get; set; }

    string IDialogAware.Title => string.Empty;

    event AsyncEventHandler<DialogCloseEventArgs>? IDialogAware.RequestCloseAsync
    {
        add { }
        remove { }
    }

    Task IDialogAware.OnDialogClosingAsync(IDialogResult? dialogResult, CancellationToken cancellationToken) => Task.CompletedTask;

    Task IDialogAware.OnDialogClosedAsync(IDialogResult? dialogResult, CancellationToken cancellationToken) => Task.CompletedTask;
}