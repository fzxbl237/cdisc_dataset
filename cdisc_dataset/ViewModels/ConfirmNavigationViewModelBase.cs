using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels;

public class ConfirmNavigationViewModelBase:ObservableObject,IConfirmNavigationRequest
{
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