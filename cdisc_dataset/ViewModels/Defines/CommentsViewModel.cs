using AsyncNavigation;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
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
using DynamicData;
using DynamicData.Binding;
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
    private bool _isErrorOnly;

    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _documentOptions = [];
    
    private FrozenDictionary<string,Document>? _frozenDocumentDictionary;

    private readonly SourceCache<CommentDto, int> _sourceCache = new(o => o.Id);
    private readonly ReadOnlyObservableCollection<CommentDto> _comments;
    public ReadOnlyObservableCollection<CommentDto> Comments => _comments;

    public CommentsViewModel(
        IMessageService messageService,
        ICommentService commentService,
        IDocumentService documentService,
        IIssueService issueService,
        IDialogHostService dialogHostService,
        cdisc_dataset.Services.IDialogService dialogService,
        ICurrentProjectService currentProjectService,
        IMapper mapper,
        IValidator<CommentDto> validator,
        ILookupStore lookupStore)
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

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(_ => BuildFilter());
        _sourceCache.Connect()
            .AutoRefresh(o => o.HasErrors)
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _comments, SortExpressionComparer<CommentDto>.Ascending(o => o.UniqueId ?? string.Empty))
            .DisposeMany()
            .Subscribe();

        lookupStore.Documents
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildDocumentLookups);
    }

    partial void OnIsErrorOnlyChanged(bool value) => _sourceCache.Refresh();

    private Func<CommentDto, bool> BuildFilter()
    {
        var searchFilter = SearchFilterExtensions.BuildSearchFilter<CommentDto>(
            SearchText,
            x => x.UniqueId,
            x => x.Description,
            x => x.DocumentUniqueId,
            x => x.Pages);
        return comment => (!IsErrorOnly || comment.HasErrors) && searchFilter(comment);
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
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.Pages),nameof(CommentDto.DocumentUniqueId));
                    break;
                case nameof(CommentDto.Pages):
                    await _validator.ValidateDtoAsync(commentDto, nameof(CommentDto.Pages), nameof(CommentDto.DocumentUniqueId));
                    break;
            }
            _sourceCache.AddOrUpdate(commentDto);
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
    private async Task AddDocumentAsync(CommentDto commentDto)
    {
        var result = await _dialogService.ShowAddDocumentModelAsync(new DocumentDto());
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var documentDto))
            return;

        var inserted = await _documentService.InsertDocumentAsync(documentDto);
        commentDto.Document = _mapper.Map<Document>(inserted);
        commentDto.DocumentId = inserted.Id;
        commentDto.DocumentUniqueId = inserted.UniqueId;
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
        commentDto.Document = _mapper.Map<Document>(documentDto);
        commentDto.DocumentId = documentDto.Id;
        commentDto.DocumentUniqueId = documentDto.UniqueId;
    }

    private void ApplyDocument(CommentDto commentDto, string? documentUniqueId)
    {
        if (documentUniqueId == commentDto.DocumentUniqueId) return;
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
    private async Task AddCommentAsync()
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
        var insertedComment = await _commentService.InsertCommentAsync(commentDto);
        await _validator.ValidateDtoAsync(insertedComment);
        RegisterCommentDtoPropertyChanged(insertedComment);
        _sourceCache.AddOrUpdate(insertedComment);
        MarkDuplicates();
        _messageService.Success("Comment added successfully.");
    }
    
    [RelayCommand]
    private async Task EditCommentAsync(CommentDto comment)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "Model", _mapper.Map<CommentDto>(comment) }
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog",dialogParameters);
        if (!result.Parameters.TryGetValue<CommentDto>("Model", out var commentDto) || _currentProjectService.CurrentProject == null)
            return;
        await _commentService.UpdateCommentAsync(commentDto);
        _sourceCache.AddOrUpdate(commentDto);
        MarkDuplicates();
        HasChanges = false;
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
        _sourceCache.Remove(commentDto);
        MarkDuplicates();
        _messageService.Success("Comment deleted successfully.");
    }

    private void MarkDuplicates()
    {
        var comments = _sourceCache.Items;
        foreach (var comment in comments)
        {
            comment.IsUniqueIdDuplicate = false;
            comment.IsDescriptionDuplicate = false;
        }

        comments.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (comment, isDuplicate) => comment.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        comments.MarkDuplicates(
            o => o.Description ?? string.Empty,
            (comment, isDuplicate) => comment.IsDescriptionDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_currentProjectService.CurrentProject == null)
            return;
        await _commentService.SaveCommentsAsync(_sourceCache.Items.ToList());
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
    }

    public override async Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var commentDto in _sourceCache.Items)
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
        foreach (var commentDto in _sourceCache.Items)
            UnregisterCommentDtoPropertyChanged(commentDto);

        var list = await _commentService.GetAllCommentDtosAsync();
        foreach (var commentDto in list)
        {
            await _validator.ValidateDtoAsync(commentDto);
            RegisterCommentDtoPropertyChanged(commentDto);
        }

        _sourceCache.Edit(cache =>
        {
            cache.Clear();
            cache.AddOrUpdate(list);
        });
        MarkDuplicates();
        HasChanges = false;
    }
    
    private void RebuildDocumentLookups(IReadOnlyCollection<Document> documents)
    {
        DocumentOptions.Clear();
        DocumentOptions.AddRange(documents.Select(document => new DocumentAutoCompleteOption
        {
            Header = $"{document.UniqueId} {document.Title}",
            Content = document.UniqueId,
            Document = document
        }));

        _frozenDocumentDictionary = documents
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
    }
}
