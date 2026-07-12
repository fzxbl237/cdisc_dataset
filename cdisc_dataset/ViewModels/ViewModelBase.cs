using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels;

public partial class ViewModelBase:ObservableObject,INavigationAware
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
}