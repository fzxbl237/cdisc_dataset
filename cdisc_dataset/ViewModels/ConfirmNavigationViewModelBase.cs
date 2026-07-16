using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels;

public partial class ConfirmNavigationViewModelBase:ObservableObject,IConfirmNavigationRequest
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

    public virtual void OnNavigatedTo(NavigationContext navigationContext)
    {
        
    }

    public virtual bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    public virtual void OnNavigatedFrom(NavigationContext navigationContext)
    {
        
    }

    public virtual void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }
}