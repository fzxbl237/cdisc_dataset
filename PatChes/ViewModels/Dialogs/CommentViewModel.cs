using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services;
using PatChes.Services.Interface;
using PatChes.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

public partial class CommentViewModel(
    IMessageService messageService,
    IDocumentService documentService,
    IVariableService variableService,
    FormCommentValidator formCommentValidator,
    ICurrentProjectService currentProjectService,
    IValidator<CommentDto> validator)
    : ObservableObject, IDialogHostAware
{
    private readonly IValidator<CommentDto> _validator = validator;
    private readonly IVariableService _variableService = variableService;

    private FrozenDictionary<string, Document>? _frozenDocumentDictionary;

    public string? DialogHostName { get; set; }

    public DefaultFilterValueSelector VariableFilterValueSelector { get; } = data =>
        (data as IListItemData)?.Content;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private CommentDto _comment = new();

    [ObservableProperty]
    private CommentDocumentSelectOption? _selectedDocumentOption;

    [ObservableProperty]
    private bool _isInEditMode;

    [ObservableProperty]
    private IList<IFormValidator> _validators = [];

    [ObservableProperty]
    private List<IListItemData> _variables = [];

    [ObservableProperty]
    private ObservableCollection<EntityKey> _targetKeys = [];

    public AvaloniaList<ISelectOption> DocumentOptions { get; } = [];

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        parameters.TryGetValue("Title", out string? title);
        Title = title;
        parameters.TryGetValue("Model", out CommentDto? model);
        Comment = model??new CommentDto();
        IsInEditMode = Comment.Id != 0;
        formCommentValidator.IsInEditMode = IsInEditMode;
        Comment.ProjectId = currentProjectService.CurrentProject?.Id??0;
        Comment.CdiscDataType = currentProjectService.CdiscDataType;
        formCommentValidator.CommentDto = Comment;
        Validators.Add(formCommentValidator);
        await LoadDocuments();
        await LoadVariables();
    }

    partial void OnSelectedDocumentOptionChanged(CommentDocumentSelectOption? value)
    {
        if (value?.Document == null)
        {
            Comment.Document = null;
            Comment.DocumentId = 0;
            Comment.DocumentUniqueId = null;
            return;
        }

        Comment.Document = value.Document;
        Comment.DocumentId = value.Document.Id;
        Comment.DocumentUniqueId = value.Document.UniqueId;
    }

    private async Task LoadVariables()
    {
        var variables = await _variableService.GetAllVariableDtosAsync();
        Variables = variables
            .Where(variable => variable.CommentId == 0)
            .OrderBy(variable => variable.DatasetName)
            .ThenBy(variable => variable.Order)
            .ThenBy(variable => variable.VariableName)
            .Select(variable => (IListItemData)new ListItemData
            {
                ItemKey = variable.Id.ToString(),
                Content = $"{variable.DatasetName}.{variable.VariableName} - {variable.Label}"
            })
            .ToList();
        TargetKeys.Clear();
    }

    private async Task LoadDocuments()
    {
        var documents = await documentService.GetAllDocumentsWithoutErorrAsync();
        _frozenDocumentDictionary = documents
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        List<ISelectOption> options = [];
        foreach (var document in documents)
        {
            var option = new CommentDocumentSelectOption
            {
                Header = document.UniqueId,
                Content = $"{document.UniqueId} {document.Title}",
                Title = document.Title,
                Document = document
            };
            options.Add(option);
        }

        DocumentOptions.Clear();
        DocumentOptions.AddRange(options);

        if (!string.IsNullOrWhiteSpace(Comment.DocumentUniqueId)
            && _frozenDocumentDictionary.TryGetValue(Comment.DocumentUniqueId, out var selectedDocument))
        {
            SelectedDocumentOption = DocumentOptions
                .OfType<CommentDocumentSelectOption>()
                .FirstOrDefault(o => o.Document?.Id == selectedDocument.Id);
            Comment.Document = selectedDocument;
            Comment.DocumentId = selectedDocument.Id;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        // var validationResult = await _validator.ValidateAsync(Comment);
        // if (!validationResult.IsValid)
        // {
        //     _messageManager.ShowError(validationResult.Errors.First().ErrorMessage);
        //     return;
        // }

        var dialogResult = new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "Model", Comment },
                {
                    "VariableIds",
                    TargetKeys
                        .Select(key => int.TryParse(key.Value, out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList()
                }
            }
        };
        DialogHost.Close(DialogHostName ?? "Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}

public class CommentDocumentSelectOption : SelectOption
{
    public string? Title { get; set; }
    public Document? Document { get; set; }
}