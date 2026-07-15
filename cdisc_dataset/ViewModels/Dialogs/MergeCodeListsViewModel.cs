using System.Collections.Generic;
using System.Linq;
using AtomUI.Controls;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class MergeCodeListsViewModel : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; }

    [ObservableProperty]
    private string? _uniqueId;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private AvaloniaList<Term> _terms = [];

    private CodeListDto? _retainedCodeList;

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (!parameters.TryGetValue<List<CodeListDto>>("CodeLists", out var codeLists) || codeLists.Count == 0)
            return;

        _retainedCodeList = codeLists[0];
        UniqueId = _retainedCodeList.UniqueId;
        Name = _retainedCodeList.Name;
        Code = _retainedCodeList.Code;

        Terms.Clear();
        Terms.AddRange(codeLists
            .SelectMany(o => o.Terms ?? [])
            .GroupBy(o => (o.Name, o.Code, o.DecodedValue))
            .Select((group, index) =>
            {
                var term = group.First();
                term.Order = index + 1;
                return term;
            }));
    }

    [RelayCommand]
    private void Confirm()
    {
        if (_retainedCodeList == null)
            return;

        var mergedCodeList = new CodeListDto
        {
            Id = _retainedCodeList.Id,
            UniqueId = UniqueId,
            Name = Name,
            Code = _retainedCodeList.Code,
            Type = _retainedCodeList.Type,
            Terminology = _retainedCodeList.Terminology,
            CommentId = _retainedCodeList.CommentId,
            Comment = _retainedCodeList.Comment,
            CommentUniqueId = _retainedCodeList.CommentUniqueId,
            DeveloperNotes = _retainedCodeList.DeveloperNotes,
            Terms = Terms.ToList(),
            CdiscDataType = _retainedCodeList.CdiscDataType,
            ProjectId = _retainedCodeList.ProjectId
        };

        DialogHost.Close("Root", new DialogResult
        {
            Result = ButtonResult.OK,
            Parameters = new DialogParameters { { "MergedCodeList", mergedCodeList } }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}
