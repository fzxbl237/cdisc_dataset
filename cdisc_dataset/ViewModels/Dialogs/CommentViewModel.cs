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

public partial class CommentViewModel : ObservableObject, IDialogHostAware
{
    private readonly WindowMessageManager _messageManager;
    private readonly IDocumentService _documentService;
    private readonly FormCommentValidator _formCommentValidator;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<CommentDto> _validator;

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

    [ObservableProperty]
    private DefaultFilterValueSelector _valueFilterPropertySelector = data =>
    {
        if (data is ISelectOption option)
        {
            return option.Content;
        }

        return null;
    };

    public AvaloniaList<ISelectOption> DocumentOptions { get; } = [];

    public CommentViewModel(
        WindowMessageManager messageManager,
        IDocumentService documentService,
        FormCommentValidator formCommentValidator,
        ICurrentProjectService  currentProjectService,
        IValidator<CommentDto> validator)
    {
        _messageManager = messageManager;
        _documentService = documentService;
        _formCommentValidator = formCommentValidator;
        _currentProjectService = currentProjectService;
        _validator = validator;
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");

        Comment = parameters.ContainsKey("Model") ? parameters.GetValue<CommentDto>("Model") : new CommentDto();
        IsInEditMode = Comment.Id != 0;
        _formCommentValidator.IsInEditMode = IsInEditMode;
        Comment.ProjectId = _currentProjectService.CurrentProject?.Id??0;
        Comment.CdiscDataType = _currentProjectService.CdiscDataType;
        _formCommentValidator.CommentDto = Comment;
        Validators.Add(_formCommentValidator);
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
        var documents = await _documentService.GetAllDocumentsWithoutErorrAsync();
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

public record CommentDocumentSelectOption : SelectOption
{
    public string? Title { get; set; }
    public Document? Document { get; set; }
}