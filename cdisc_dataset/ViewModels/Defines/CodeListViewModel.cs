using AsyncNavigation;
using System;
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
using cdisc_dataset.Controls.DataGrid;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dm.util;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class CodeListViewModel:ConfirmNavigationViewModelBase
{
    private readonly ICodeListService _codeListService;
    private readonly ICommentService _commentService;
    private readonly IReferenceDeletionService _referenceDeletionService;
    private readonly IDialogHostService _dialogHostService;
    private readonly cdisc_dataset.Services.IDialogService _dialogService;
    private readonly IMessageService _messageService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IVariableService _variableService;
    private readonly IMapper _mapper;
    private readonly IValidator<CodeListDto> _validator;
    public AvaloniaList<string> Yns { get; set; } = ["Yes", "No"]; 
    public AvaloniaList<string> DataTypes { get; set; } = ["text", "integer", "float","datetime","date","time",
        "partialDate","partialTime","partialDateTime","incompleteDatetime","durationDatetime","intervalDatetime"];
    
    public AvaloniaList<string> Origins { get; set; } = [];
    
    public AvaloniaList<string> Sources {get;set;} = ["","Investigator","Subject"];
    
    [ObservableProperty]
    private AvaloniaList<Comment> _comments = [];
    
    [ObservableProperty]
    private AvaloniaList<string?> _terminologies = [];
    
    
    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _commentOptions = [];
    

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _isErrorOnly;
    
    [ObservableProperty] private bool _hasChanges;
    private readonly SourceCache<CodeListDto,int> _sourceCache = new(o=>o.Id);
    
    private readonly ReadOnlyObservableCollection<CodeListDto> _codeLists;
    public ReadOnlyObservableCollection<CodeListDto> CodeLists => _codeLists;

    public CodeListViewModel(ICodeListService codeListService,
        ICommentService commentService,
        IReferenceDeletionService referenceDeletionService,
        IDialogHostService dialogHostService,
        cdisc_dataset.Services.IDialogService dialogService,
        IMessageService messageService,
        ICurrentProjectService currentProjectService,
        IVariableService variableService,
        IMapper mapper,
        IValidator<CodeListDto> validator,
        ILookupStore lookupStore)
    {
        _codeListService = codeListService;
        _commentService = commentService;
        _referenceDeletionService = referenceDeletionService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _messageService = messageService;
        _currentProjectService = currentProjectService;
        _variableService = variableService;
        _mapper = mapper;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(_ => BuildFilter());
        _sourceCache.Connect()
            .AutoRefresh(o => o.HasErrors)
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _codeLists,SortExpressionComparer<CodeListDto>.Ascending(o => o.UniqueId??string.Empty))
            .DisposeMany()
            .Subscribe();

        lookupStore.Comments
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildCommentLookups);
    }

    private void MarkDuplicates()
    {
        var codeLists = _sourceCache.Items.ToList();
        foreach (var codeList in codeLists)
        {
            codeList.IsUniqueIdDuplicate = false;
            codeList.IsNameDuplicate = false;
        }

        codeLists.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (codeList, isDuplicate) => codeList.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        codeLists.MarkDuplicates(
            o => o.Name ?? string.Empty,
            (codeList, isDuplicate) => codeList.IsNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    partial void OnIsErrorOnlyChanged(bool value) => _sourceCache.Refresh();

    private Func<CodeListDto, bool> BuildFilter()
    {
        var searchText = SearchText;
        if (string.IsNullOrEmpty(searchText)) return o => !IsErrorOnly || o.HasErrors;
        return o => (!IsErrorOnly || o.HasErrors) && (Contains(searchText, o.UniqueId)
                    || Contains(searchText, o.Name)
                    || Contains(searchText,o.Code)
                    || Contains(searchText,o.Type)
                    || Contains(searchText,o.Terminology)
                    || Contains(searchText,o.CommentUniqueId));
    }

    private static bool Contains(string? searchText, string? value)
    {
        return (!string.IsNullOrWhiteSpace(value) && value.Contains(searchText!, StringComparison.OrdinalIgnoreCase));
    }
    
    public async Task LoadCodeLists()
    {
        // ?????????? PropertyChanged ????
        foreach (var codeListDto in _sourceCache.Items)
        {
            codeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        }

        var list = await _codeListService.GetAllCodeListDtosAsync();
        foreach (var codeListDto in list)
        {
            await _validator.ValidateDtoAsync(codeListDto);
            codeListDto.PropertyChanged += CodeListDtoOnPropertyChanged;
        }
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
        MarkDuplicates();
    }

    private void CodeListDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CodeListDto codeListDto) return;

        var duplicateFlagProperty = e.PropertyName switch
        {
            nameof(CodeListDto.IsUniqueIdDuplicate) => nameof(CodeListDto.UniqueId),
            nameof(CodeListDto.IsNameDuplicate) => nameof(CodeListDto.Name),
            _ => null
        };

        if (duplicateFlagProperty != null)
        {
            Observable.StartAsync(() => _validator.ValidateDtoAsync(codeListDto, duplicateFlagProperty));
            return;
        }

        if (e.PropertyName is nameof(CodeListDto.CommentUniqueId))
            HandleCommentUniqueIdChanged(codeListDto);

        if (e.PropertyName is not (
                nameof(CodeListDto.UniqueId) or
                nameof(CodeListDto.Name) or
                nameof(CodeListDto.Code) or
                nameof(CodeListDto.Type) or
                nameof(CodeListDto.Terminology) or
                nameof(CodeListDto.CommentUniqueId) or
                nameof(CodeListDto.DeveloperNotes)))
        {
            return;
        }
        Observable.StartAsync(async () =>
        {
            if (e.PropertyName is nameof(CodeListDto.UniqueId) or nameof(CodeListDto.Name))
                MarkDuplicates();

            await _validator.ValidateDtoAsync(codeListDto, e.PropertyName);
            _sourceCache.AddOrUpdate(codeListDto);
        });
        codeListDto.HasChanged = true;
        HasChanges = true;
    }

    private void HandleCommentUniqueIdChanged(CodeListDto codeListDto)
    {
        var changeValue = codeListDto.CommentUniqueId;
        var first = Comments.FirstOrDefault(o => o.UniqueId == changeValue);
        if (first != null)
        {
            codeListDto.CommentId = first.Id;
            codeListDto.Comment = first;
        }

        if (string.IsNullOrWhiteSpace(changeValue))
        {
            codeListDto.CommentId = 0;
            codeListDto.Comment = null;
        }
        // _sourceCache.AddOrUpdate(codeListDto);
    }
    
    private void RebuildCommentLookups(IReadOnlyCollection<Comment> comments)
    {
        Comments.Clear();
        Comments.AddRange(comments);

        CommentOptions.Clear();
        CommentOptions.AddRange(comments.Select(comment => new AutoCompleteOption
        {
            Header = comment.Description,
            Content = comment.UniqueId
        }));
    }

    public async Task LoadTerminologies()
    {
        var terminologies = await _codeListService.GetTerminologiesAsync();
        Terminologies.AddRange(terminologies);
    }
    
    [RelayCommand]
    private async Task MergeSelectedCodeListsAsync()
    {
        var selectedCodeLists = CodeLists.Where(o => o.IsSelected).ToList();
        if (selectedCodeLists.Count < 2)
        {
            _messageService.Error("Please select at least two code lists to merge.");
            return;
        }

        if (selectedCodeLists.Select(o => o.Code).Distinct().Count() != 1)
        {
            _messageService.Error("Selected NCI codes must be identical before merging.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("MergeCodeListsDialog", new DialogParameters
        {
            { "CodeLists", selectedCodeLists }
        });
        if (result.Result != DialogButtonResult.OK
            || !result.Parameters.TryGetValue<CodeListDto>("MergedCodeList", out var mergedCodeList))
        {
            return;
        }

        await _codeListService.MergeCodeListsAsync(mergedCodeList, selectedCodeLists.Select(o => o.Id).ToList());
        await LoadCodeLists();
        HasChanges = false;
        _messageService.Success("Code lists merged successfully.");
    }

    [RelayCommand]
    private async Task EditCodeListAsync(CodeListDto? codeList)
    {
        if (codeList == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("CodeListDialog", new DialogParameters
        {
            { "Model", _mapper.Map<CodeListDto>(codeList) }
        });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CodeListDto>("Model", out var updatedCodeList))
        {
            return;
        }

        await _codeListService.UpdateCodeListAsync(updatedCodeList);
        _sourceCache.AddOrUpdate(updatedCodeList);
        MarkDuplicates();
        _messageService.Success("Code list updated successfully.");
    }

    [RelayCommand]
    private async Task EditTermsAsync(CodeListDto? codeList)
    {
        if (codeList == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("EditTermsDialog", new DialogParameters
        {
            { "Model", codeList }
        });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CodeListDto>("Model", out var updatedCodeList))
        {
            return;
        }

        await _codeListService.UpdateCodeListWithTermsAsync(updatedCodeList);
        codeList.Terms = updatedCodeList.Terms;
        _sourceCache.AddOrUpdate(codeList);
        _messageService.Success("Terms updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteAsync(CodeListDto codeList)
    {
        var clearReferences = await _referenceDeletionService.ConfirmReferenceDeletionAsync(
            $"Delete code list {codeList.Name}?",
            "Code list",
            await _codeListService.ConfirmCodeListReferenceAsync(codeList));
        if (clearReferences == null)
            return;

        await _codeListService.DeleteCodeListAsync(codeList, clearReferences.Value);
        _sourceCache.Edit(o =>
        {
            o.Remove(codeList);
        });
        MarkDuplicates();
        _messageService.Success("Code list deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedCodeLists = _sourceCache.Items.Where(o => o.IsSelected).ToList();
        if (selectedCodeLists.Count == 0)
        {
            _messageService.Info("Please select at least one code list to delete.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Code Lists" },
            { "Message", $"Are you sure you want to delete {selectedCodeLists.Count} selected code list(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        foreach (var codeList in selectedCodeLists)
            await _codeListService.DeleteCodeListAsync(codeList);

        _sourceCache.Remove(selectedCodeLists);
        MarkDuplicates();
        _messageService.Success($"{selectedCodeLists.Count} code list(s) deleted successfully.");
    }
    
    [RelayCommand]
    private async Task AddComment(CodeListDto codeList)
    {
        var result = await _dialogService.ShowAddCommentModelAsync($"COM.CL.{codeList.UniqueId}");
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CommentDto>("Model", out var comment))
            return;

        var entity = await _commentService.InsertCommentAsync(comment);
        codeList.Comment = _mapper.Map<Comment>(entity);
        codeList.CommentId = entity.Id;
        codeList.CommentUniqueId = entity.UniqueId;
        _sourceCache.Edit(o => o.AddOrUpdate(codeList));
        var updateResult = await _codeListService.UpdateCodeListAsync(codeList);
        if (updateResult > 0)
            _messageService.Success("Comment added successfully.");
    }
    
    [RelayCommand]
    private async Task EditCommentAsync(Comment comment)
    {
        var result = await _dialogService.ShowEditCommentModelAsync(_mapper.Map<CommentDto>(comment));
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CommentDto>("Model", out var updatedComment))
            return;

        await _commentService.UpdateCommentAsync(updatedComment);
        _messageService.Success("Comment updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteComment(Comment? comment)
    {
        if (comment == null || !await _referenceDeletionService.ConfirmAndDeleteCommentAsync(comment))
            return;

        var codeLists = CodeLists.Where(o => o.CommentId == comment.Id).ToList();
        foreach (var codeList in codeLists)
        {
            codeList.CommentId = 0;
            codeList.CommentUniqueId = string.Empty;
            codeList.Comment = null;
        }
        _sourceCache.Edit(o => o.AddOrUpdate(codeLists));
        _messageService.Success("Comment deleted successfully.");
    }
    
    
    [RelayCommand]
    private async Task AddCodeListFromVariable()
    {
        var result = await _dialogHostService.ShowDialogAsync("CodeListDialog", new DialogParameters
        {
            { "SelectVariable", true }
        });
        if (!result.Parameters.TryGetValue<CodeList>("CodeList", out var codeList) || codeList == null)
            return;

        var entity = await _codeListService.InsertCodeListAsync(codeList);
        if (result.Parameters.TryGetValue<VariableDto>("Variable", out var variable) && variable != null)
        {
            variable.CodeListId = entity.Id;
            variable.CodeListUniqueId = entity.UniqueId;
            variable.CodeList = _mapper.Map<CodeList>(entity);
            await _variableService.UpdateVariableAsync(variable);
        }

        await _validator.ValidateDtoAsync(entity);
        entity.PropertyChanged += CodeListDtoOnPropertyChanged;
        _sourceCache.Edit(o => o.AddOrUpdate(entity));
        MarkDuplicates();
        _messageService.Success("Code list created and linked to the variable successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        await _codeListService.SaveCodeListsAsync(CodeLists.ToList());
        _messageService.Success("Code lists saved successfully.");
        HasChanges = false;
    }
    
    [RelayCommand]
    private async Task Discard()
    {
        if(!HasChanges) return;
        await LoadCodeLists();
        HasChanges = false;
        
    }
    
    
    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        // var navigationContextParameters = navigationContext.Parameters;
        // navigationContextParameters.TryGetValue("CdiscDataType",out CdiscDataType cdiscDataType);
        // CdiscDataType = cdiscDataType;
        // if (_currentProjectService.CurrentProject != null)
        // {
        //     //LoadCodeLists(_currentProjectService.CurrentProject.Id,CdiscDataType).Await();
        //     //LoadComments().Await();
        // }
        //LoadTerminologies().Await();
        return Task.CompletedTask;
    }


    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        // ??????? CodeListDto ?? PropertyChanged ????
        foreach (var codeListDto in _sourceCache.Items)
        {
            codeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        }

        if(!HasChanges) return Task.CompletedTask;
        _codeListService.SaveCodeListsAsync(CodeLists.ToList()).AwaitWithOpt();
        _messageService.Success("Code lists saved successfully.");
        return Task.CompletedTask;
    }
    
}
