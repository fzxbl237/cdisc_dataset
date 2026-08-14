using AsyncNavigation;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using MapsterMapper;
using Prism.Dialogs;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class CommentsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly ICommentService _commentService;
    private readonly IDocumentService _documentService;
    private readonly IIssueService _issueService;
    private readonly IDialogHostService _dialogHostService;
    private readonly cdisc_dataset.Services.IDialogService _dialogService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMapper _mapper;
    private readonly IValidator<CommentDto> _validator;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _documentOptions = [];
    
    private FrozenDictionary<string,Document>? _frozenDocumentDictionary;

    public AvaloniaList<CommentDto> Comments { get; } = [];

    public CommentsViewModel(
        IMessageService messageService,
        ICommentService commentService,
        IDocumentService documentService,
        IIssueService issueService,
        IDialogHostService dialogHostService,
        cdisc_dataset.Services.IDialogService dialogService,
        ICurrentProjectService currentProjectService,
        IMapper mapper,
        IValidator<CommentDto> validator)
    {
        _messageService = messageService;
        _commentService = commentService;
        _documentService = documentService;
        _issueService = issueService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _currentProjectService = currentProjectService;
        _mapper = mapper;
        _validator = validator;

    }

    private void CommentDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CommentDto commentDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(CommentDto.HasChanged))
            return;

        var duplicateFlagProperty = e.PropertyName switch
        {
            nameof(CommentDto.IsUniqueIdDuplicate) => nameof(CommentDto.UniqueId),
            nameof(CommentDto.IsDescriptionDuplicate) => nameof(CommentDto.Description),
            _ => null
        };

        if (duplicateFlagProperty != null)
        {
            Observable.StartAsync(() => _validator.ValidateDtoAsync(commentDto, duplicateFlagProperty));
            return;
        }

        if (e.PropertyName is not (
                nameof(CommentDto.UniqueId) or
                nameof(CommentDto.Description) or
                nameof(CommentDto.DocumentUniqueId) or
                nameof(CommentDto.Pages)))
        {
            return;
        }

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(CommentDto.UniqueId):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.UniqueId));
                    break;
                case nameof(CommentDto.Description):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.Description));
                    break;
                case nameof(CommentDto.DocumentUniqueId):
                    ApplyDocument(commentDto, commentDto.DocumentUniqueId);
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.Pages));
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.DocumentUniqueId));
                    break;
                case nameof(CommentDto.Pages):
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.Pages));
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.DocumentUniqueId));
                    break;
            }
        });

        commentDto.HasChanged = true;
        HasChanges = true;
    }

    private void RegisterCommentDtoPropertyChanged(CommentDto commentDto)
    {
        commentDto.PropertyChanged += CommentDtoOnPropertyChanged;
    }

    private void UnregisterCommentDtoPropertyChanged(CommentDto commentDto)
    {
        commentDto.PropertyChanged -= CommentDtoOnPropertyChanged;
    }

    [RelayCommand]
    private async Task AddDocument(CommentDto commentDto)
    {
        var result = await _dialogService.ShowAddDocumentModelAsync(new DocumentDto());
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var documentDto))
            return;

        await _documentService.InsertDocumentAsync(documentDto);
        await LoadDocuments();
        ApplyDocument(commentDto, documentDto.UniqueId);
    }

    [RelayCommand]
    private async Task EditDocumentAsync(CommentDto commentDto)
    {
        if (commentDto.Document == null)
            return;

        var result = await _dialogService.ShowEditDocumentModelAsync(_mapper.Map<DocumentDto>(commentDto.Document));
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var documentDto))
            return;

        await _documentService.UpdateDocumentAsync(documentDto);
        await LoadDocuments();
        ApplyDocument(commentDto, documentDto.UniqueId);
    }

    private void ApplyDocument(CommentDto commentDto, string? documentUniqueId)
    {
        if (string.IsNullOrWhiteSpace(documentUniqueId) || _frozenDocumentDictionary == null ||
            !_frozenDocumentDictionary.TryGetValue(documentUniqueId, out var document))
        {
            commentDto.Document = null;
            commentDto.DocumentId = 0;
            return;
        }

        commentDto.Document = document;
        commentDto.DocumentId = document.Id;
        commentDto.DocumentUniqueId = document.UniqueId;
    }

    [RelayCommand]
    private async Task AddComment()
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Comment" }
        };


        var result = await _dialogHostService.ShowDialogAsync("CommentDialog", dialogParameters);
        if (!result.Parameters.TryGetValue<CommentDto>("Model", out var commentDto) || _currentProjectService.CurrentProject == null)
            return;

        commentDto.ProjectId = _currentProjectService.CurrentProject.Id;
        commentDto.CdiscDataType = _currentProjectService.CdiscDataType;
        await _commentService.InsertCommentAsync(commentDto);
        await _validator.ValidateDtoAsync(commentDto);
        RegisterCommentDtoPropertyChanged(commentDto);
        Comments.Add(commentDto);
        MarkDuplicates();
        _messageService.Success("Comment added successfully.");
    }
    
    [RelayCommand]
    private async Task EditCommentAsync(CommentDto comment)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "Model", comment }
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog",dialogParameters);
        if (!result.Parameters.TryGetValue<CommentDto>("Model", out var commentDto) || _currentProjectService.CurrentProject == null)
            return;
        await _commentService.UpdateCommentAsync(commentDto);
        _messageService.Success("Comment updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteAsync(CommentDto commentDto)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Comment" },
            { "Message", $"Are you sure you want to delete comment {commentDto.UniqueId}?" }
        });
        if (result.Result != ButtonResult.OK)
            return;

        if (_currentProjectService.CurrentProject == null)
            return;

        var comment = (await _commentService.GetAllCommentsAsync())
            .FirstOrDefault(o => o.Id == commentDto.Id);
        if (comment == null)
            return;

        await _commentService.DeleteCommentAsync(comment);
        UnregisterCommentDtoPropertyChanged(commentDto);
        Comments.Remove(commentDto);
        MarkDuplicates();
        _messageService.Success("Comment deleted successfully.");
    }

    private void MarkDuplicates()
    {
        foreach (var comment in Comments)
        {
            comment.IsUniqueIdDuplicate = false;
            comment.IsDescriptionDuplicate = false;
        }

        Comments.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (comment, isDuplicate) => comment.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        Comments.MarkDuplicates(
            o => o.Description ?? string.Empty,
            (comment, isDuplicate) => comment.IsDescriptionDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_currentProjectService.CurrentProject == null)
            return;
        await _commentService.SaveCommentsAsync(Comments.ToList());
        HasChanges = false;
        _messageService.Success("Comments saved successfully.");
        await LoadComments();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        await LoadComments();
        HasChanges = false;
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await LoadComments();
        await LoadDocuments();
    }

    public override async Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var commentDto in Comments)
            UnregisterCommentDtoPropertyChanged(commentDto);

        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        var dialogParameters = new DialogParameters
        {
            { "Title", "You have unsaved changes" },
            { "Message", "Do you want to save changes before leaving?" }
        };

        var result = await _dialogHostService.ShowDialogAsync("UnsavedChangesDialog", dialogParameters);
        if (result.Result == ButtonResult.OK)
        {
            await SaveCommand.ExecuteAsync(null);
            return;
        }

        if (result.Result == ButtonResult.No)
        {
            await DiscardCommand.ExecuteAsync(null);
        }
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        //continuationCallback(!Comments.Any(o => o.HasErrors));
        continuationCallback(true);
    }

    public async Task LoadComments()
    {
        foreach (var commentDto in Comments)
            UnregisterCommentDtoPropertyChanged(commentDto);

        var list = await _commentService.GetAllCommentDtosAsync();
        foreach (var commentDto in list)
        {
            await _validator.ValidateDtoAsync(commentDto);
            RegisterCommentDtoPropertyChanged(commentDto);
        }

        Comments.Clear();
        Comments.AddRange(list.OrderBy(comment => comment.UniqueId, StringComparer.OrdinalIgnoreCase));
        MarkDuplicates();
        HasChanges = false;
    }
    
    public async Task LoadDocuments()
    {
        var list = await _documentService.GetAllDocumentsWithoutErorrAsync();
        List<IAutoCompleteOption> res = [];
        foreach (var document in list)
        {
            var documentAutoCompleteOption = new DocumentAutoCompleteOption()
            {
                Header = $"{document.UniqueId} {document.Title}",
                Content = document.UniqueId,
                Document = document
            };
            res.Add(documentAutoCompleteOption);
        }
        DocumentOptions.Clear();
        DocumentOptions.AddRange(res);
        _frozenDocumentDictionary=list.Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        foreach (var commentDto in Comments)
            ApplyDocument(commentDto, commentDto.DocumentUniqueId);
    }
}
