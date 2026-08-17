using System;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Defines;

public partial class IssueViewModel : ConfirmNavigationViewModelBase
{
    private readonly IIssueService _issueService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMessageService _messageService;
    private readonly IDefineXmlValidationService _defineXmlValidationService;

    [ObservableProperty]
    private AvaloniaList<IssueDto> _issues = [];

    public IssueViewModel(
        IIssueService issueService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IMessageService messageService,
        IDefineXmlValidationService defineXmlValidationService)
    {
        _issueService = issueService;
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
        if (result.Result != ButtonResult.OK)
            return;

        var deletedCount = await _issueService.DeleteIssuesAsync(
            currentProject.Id,
            _currentProjectService.CdiscDataType,
            selectedIssueIds);
        await LoadDataAsync();
        _messageService.Success($"{deletedCount} issue(s) deleted successfully.");
    }
}
