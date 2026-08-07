using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncNavigation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.ViewModels;

public partial class ConfirmNavigationViewModelBase : ViewModelBase, INavigationGuard
{
    [ObservableProperty]
    private bool _isLoading;

    public async Task ExecuteLoadingAsync(Func<Task> action)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            await action();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        return Task.CompletedTask;
    }

    public override Task<bool> IsNavigationTargetAsync(NavigationContext navigationContext)
    {
        return Task.FromResult(true);
    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        return Task.CompletedTask;
    }

    public virtual void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public Task<bool> CanNavigateAsync(NavigationContext context, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfirmNavigationRequest(context, completion.SetResult);
        return completion.Task;
    }
}