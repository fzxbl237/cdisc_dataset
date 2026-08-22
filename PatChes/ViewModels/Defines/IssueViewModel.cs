using System;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Defines;

public partial class IssueViewModel : ConfirmNavigationViewModelBase
{
    private readonly IIssueService _issueService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMessageService _messageService;

    [ObservableProperty]
    private AvaloniaList<IssueDto> _issues = [];

    public IssueViewModel(
        IIssueService issueService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IMessageService messageService)
    {
        _issueService = issueService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _messageService = messageService;
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

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedIssueIds = Issues.Where(o => o.IsSelected).Select(o => o.Id).ToList();
        if (selectedIssueIds.Count == 0)
        {
            _messageService.Info("Please select at least one issue to delete.");
            return;
        }

        var currentProject = _currentProjectService.CurrentProject;
        if (currentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Issues" },
            { "Message", $"Are you sure you want to delete {selectedIssueIds.Count} selected issue(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        var deletedCount = await _issueService.DeleteIssuesAsync(
            currentProject.Id,
            _currentProjectService.CdiscDataType,
            selectedIssueIds);
        await LoadDataAsync();
        _messageService.Success($"{deletedCount} issue(s) deleted successfully.");
    }
}
