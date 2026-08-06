using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class CommentViewModel(
    IMessageService messageService,
    IDocumentService documentService,
    FormCommentValidator formCommentValidator,
    ICurrentProjectService currentProjectService,
    IValidator<CommentDto> validator)
    : ObservableObject, IDialogHostAware
{
    private readonly IValidator<CommentDto> _validator = validator;

    private FrozenDictionary<string, Document>? _frozenDocumentDictionary;

    public string? DialogHostName { get; set; }

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

    public AvaloniaList<ISelectOption> DocumentOptions { get; } = [];

    public void OnDialogOpened(IDialogParameters parameters)
    {
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
        LoadDocuments().Await();
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

        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "Model", Comment } }
        };
        DialogHost.Close(DialogHostName ?? "Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}

public class CommentDocumentSelectOption : SelectOption
{
    public string? Title { get; set; }
    public Document? Document { get; set; }
}