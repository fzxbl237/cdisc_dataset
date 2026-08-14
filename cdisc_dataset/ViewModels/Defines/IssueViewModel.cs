using System.Threading.Tasks;
using Avalonia.Collections;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace cdisc_dataset.ViewModels.Defines;

public partial class IssueViewModel : ConfirmNavigationViewModelBase
{
    private readonly IIssueService _issueService;
    private readonly ICurrentProjectService _currentProjectService;

    [ObservableProperty]
    private AvaloniaList<IssueDto> _issues = [];

    public IssueViewModel(
        IIssueService issueService,
        ICurrentProjectService currentProjectService)
    {
        _issueService = issueService;
        _currentProjectService = currentProjectService;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var currentProject = _currentProjectService.CurrentProject;
        if (currentProject == null)
        {
            Issues.Clear();
            return;
        }

        var issues = await _issueService.GetProjectIssuesAsync(
            currentProject.Id,
            _currentProjectService.CdiscDataType);
        Issues.Clear();
        Issues.AddRange(issues);
    }
}
