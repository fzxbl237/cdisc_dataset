using System;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using PatChes.Models.Dto;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Defines;

public partial class DefineIssueViewModel : ConfirmNavigationViewModelBase
{
    private readonly IDefineIssueService _defineIssueService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMessageService _messageService;
    private readonly IDefineXmlValidationService _defineXmlValidationService;

    [ObservableProperty]
    private AvaloniaList<DefineIssueDto> _issues = [];

    public DefineIssueViewModel(
        IDefineIssueService defineIssueService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IMessageService messageService,
        IDefineXmlValidationService defineXmlValidationService)
    {
        _defineIssueService = defineIssueService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _messageService = messageService;
        _defineXmlValidationService = defineXmlValidationService;
    }

    [RelayCommand]
    private async Task ValidateDefineXmlAsync()
    {
        if (_currentProjectService.CurrentProject == null)
        {
            _messageService.Error("Please select a project before validating Define XML.");
            return;
        }

        try
        {
            await ExecuteLoadingAsync(async () =>
            {
                var result = await _defineXmlValidationService.ValidateAsync();
                await LoadDataAsync();
                _messageService.Success(
                    $"Define XML validation completed: {result.ErrorCount} error(s), {result.WarningCount} warning(s).");
            });
        }
        catch (Exception exception)
        {
            _messageService.Error($"Define XML validation failed: {exception.Message}");
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var project = _currentProjectService.CurrentProject;
        if (project == null)
        {
            Issues.Clear();
            return;
        }

        var issues = await _defineIssueService.GetProjectIssuesAsync(
            project.Id,
            _currentProjectService.CdiscDataType);
        Issues.Clear();
        Issues.AddRange(issues.OrderBy(issue => issue.Pinnacle21Id, StringComparer.Ordinal));
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedIds = Issues.Where(issue => issue.IsSelected).Select(issue => issue.Id).ToList();
        if (selectedIds.Count == 0)
        {
            _messageService.Info("Please select at least one issue to delete.");
            return;
        }

        var project = _currentProjectService.CurrentProject;
        if (project == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Define Issues" },
            { "Message", $"Are you sure you want to delete {selectedIds.Count} issue(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        var deletedCount = await _defineIssueService.DeleteIssuesAsync(
            project.Id,
            _currentProjectService.CdiscDataType,
            selectedIds);
        await LoadDataAsync();
        _messageService.Success($"{deletedCount} issue(s) deleted successfully.");
    }
}
