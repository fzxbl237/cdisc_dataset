using System.Threading;
using System.Threading.Tasks;
using AsyncNavigation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PatChes.ViewModels;

public partial class ViewModelBase : ObservableObject, INavigationAware
{
    public event AsyncEventHandler<AsyncEventArgs>? AsyncRequestUnloadEvent;

    public virtual Task InitializeAsync(NavigationContext context) => Task.CompletedTask;

    public virtual Task OnNavigatedToAsync(NavigationContext context) => Task.CompletedTask;

    public virtual Task<bool> IsNavigationTargetAsync(NavigationContext context) => Task.FromResult(true);

    public virtual Task OnNavigatedFromAsync(NavigationContext context) => Task.CompletedTask;

    public virtual Task OnUnloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}