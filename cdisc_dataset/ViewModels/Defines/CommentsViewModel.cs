using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Prism.Dialogs;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class CommentsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly ICommentService _commentService;
    private readonly IDocumentService _documentService;
    private readonly IIssueService _issueService;
    private readonly IDialogHostService _dialogHostService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<CommentDto> _validator;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private CdiscDataType _cdiscDataType;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    private readonly SourceCache<CommentDto, int> _commentSourceCache = new(o => o.Id);
    
    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _documentOptions = [];
    
    private FrozenDictionary<string,Document>? _frozenDocumentDictionary;

    private readonly ReadOnlyObservableCollection<CommentDto> _comments;
    public ReadOnlyObservableCollection<CommentDto> Comments => _comments;

    public CommentsViewModel(
        IMessageService messageService,
        ICommentService commentService,
        IDocumentService documentService,
        IIssueService issueService,
        IDialogHostService dialogHostService,
        ICurrentProjectService currentProjectService,
        IValidator<CommentDto> validator)
    {
        _messageService = messageService;
        _commentService = commentService;
        _documentService = documentService;
        _issueService = issueService;
        _dialogHostService = dialogHostService;
        _currentProjectService = currentProjectService;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);

        _commentSourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _comments, SortExpressionComparer<CommentDto>
                .Ascending(o => o.UniqueId??string.Empty))
            .DisposeMany()
            .Subscribe();

        _commentSourceCache.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(_ =>
            {
                if (!HasChanges)
                    HasChanges = true;
            });

        _commentSourceCache.Connect()
            .WhenPropertyChanged(o => o.UniqueId, false)
            .Subscribe(_ =>
            {
                MarkDuplicates();
            });

        _commentSourceCache.Connect()
            .WhenPropertyChanged(o => o.HasUniqueIdDuplicate, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "UniqueId");
                    _commentSourceCache.AddOrUpdate(changeSender);
                });
            });
        
        _commentSourceCache.Connect()
            .WhenPropertyChanged(o => o.DocumentUniqueId, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                if (!string.IsNullOrWhiteSpace(changeSender.DocumentUniqueId) && _frozenDocumentDictionary!=null)
                {
                    _frozenDocumentDictionary.TryGetValue(changeSender.DocumentUniqueId, out Document? document);
                    if (document != null)
                    {
                        changeSender.Document = document;
                        changeSender.DocumentId = document.Id;
                    }
                }
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "Pages");
                    await _validator.ValidateDtoAsync(changeSender, "DocumentUniqueId");
                    _commentSourceCache.AddOrUpdate(changeSender);
                });
            });
        
        _commentSourceCache.Connect()
            .WhenPropertyChanged(o => o.Pages, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "Pages");
                    await _validator.ValidateDtoAsync(changeSender, "DocumentUniqueId");
                    _commentSourceCache.AddOrUpdate(changeSender);
                });
            });
    }

    [RelayCommand]
    private async Task AddComment()
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Comment" }
        };

        if (CurrentProject != null)
        {
            dialogParameters.Add("ProjectId", CurrentProject.Id);
            dialogParameters.Add("CdiscDataType", CdiscDataType);
        }

        var result = await _dialogHostService.ShowDialog("CommentDialog", dialogParameters);
        if (!result.Parameters.TryGetValue<CommentDto>("Model", out var commentDto) || CurrentProject == null)
            return;

        commentDto.ProjectId = CurrentProject.Id;
        commentDto.CdiscDataType = CdiscDataType;
        await _commentService.InsertCommentAsync(commentDto);
        _messageService.Success("添加成功");
        await LoadComments(CurrentProject.Id, CdiscDataType);
    }
    
    [RelayCommand]
    private async Task Modify(CommentDto comment)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "ProjectId", CurrentProject!.Id },
            { "Model", comment }
        };
        var result = await _dialogHostService.ShowDialog("CommentDialog",dialogParameters);
        if (!result.Parameters.TryGetValue<CommentDto>("Model", out var commentDto) || CurrentProject == null)
            return;
        await _commentService.UpdateCommentAsync(commentDto);
        _messageService.Success("Comment更新成功");
    }

    [RelayCommand]
    private async Task Delete(CommentDto commentDto)
    {
        if (CurrentProject == null)
            return;

        var comment = (await _commentService.GetAllCommentsAsync(CurrentProject.Id, CdiscDataType))
            .FirstOrDefault(o => o.Id == commentDto.Id);
        if (comment == null)
            return;

        await _commentService.DeleteCommentAsync(comment);
        _commentSourceCache.Remove(commentDto);
        MarkDuplicates();
        _messageService.Success("删除成功");
    }

    private void MarkDuplicates()
    {
        _commentSourceCache.Items.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (comment, isDuplicate) => comment.HasUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    [RelayCommand]
    private async Task Save()
    {
        if (CurrentProject == null)
            return;
        await _commentService.SaveCommentsAsync(Comments.ToList());
        HasChanges = false;
        _messageService.Success("Comments Save Success");
        await LoadComments(CurrentProject.Id, CdiscDataType);
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || CurrentProject == null)
            return;

        await LoadComments(CurrentProject.Id, CdiscDataType);
        HasChanges = false;
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        navigationContextParameters.TryGetValue("CurrentProject", out Project? currentProject);
        CdiscDataType = cdiscDataType;
        CurrentProject = currentProject;
        if (CurrentProject != null)
        {
            LoadComments(CurrentProject.Id, CdiscDataType).Await();
            LoadDocuments(CurrentProject.Id, CdiscDataType).Await();
        }
    }

    public override async void OnNavigatedFrom(NavigationContext navigationContext)
    {
        if (!HasChanges || CurrentProject == null)
            return;

        var dialogParameters = new DialogParameters
        {
            { "Title", "You have unsaved changes" },
            { "Message", "Do you want to save changes before leaving?" }
        };

        var result = await _dialogHostService.ShowDialog("UnsavedChangesDialog", dialogParameters);
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

    private async Task LoadComments(int id, CdiscDataType cdiscDataType)
    {
        var list = await _commentService.GetAllCommentDtosAsync(id, cdiscDataType);
        _commentSourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });

        HasChanges = false;
    }
    
    private async Task LoadDocuments(int id, CdiscDataType cdiscDataType)
    {
        var list = await _documentService.GetAllDocumentsWithoutErorrAsync(id, cdiscDataType);
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
    }

    private static Func<CommentDto, bool> BuildFilter(string? searchText)
        => SearchFilterExtensions.BuildSearchFilter<CommentDto>(
            searchText,
            x => x.UniqueId,
            x => x.Description,
            x => x.DocumentUniqueId,
            x => x.Pages);
}
