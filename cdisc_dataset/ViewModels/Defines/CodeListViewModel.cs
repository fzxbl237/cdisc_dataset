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
using Avalonia.Controls;
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
using Prism.Dialogs;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class CodeListViewModel:ConfirmNavigationViewModelBase
{
    private readonly ICodeListService _codeListService;
    private readonly ICommentService _commentService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMessageService _messageService;
    private readonly ICurrentProjectService _currentProjectService;
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
    
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty]
    private CdiscDataType _cdiscDataType;
    
    private readonly SourceCache<CodeListDto,int> _sourceCache = new(o=>o.Id);
    
    private readonly ReadOnlyObservableCollection<CodeListDto> _codeLists;
    public ReadOnlyObservableCollection<CodeListDto> CodeLists => _codeLists;

    public CodeListViewModel(ICodeListService codeListService,
        ICommentService commentService,
        IDialogHostService dialogHostService,
        IMessageService messageService,
        ICurrentProjectService currentProjectService,
        IValidator<CodeListDto> validator)
    {
        _codeListService = codeListService;
        _commentService = commentService;
        _dialogHostService = dialogHostService;
        _messageService = messageService;
        _currentProjectService = currentProjectService;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);
        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _codeLists,SortExpressionComparer<CodeListDto>.Ascending(o => o.UniqueId??string.Empty))
            .DisposeMany()
            .Subscribe();
        
    }

    private void UpdateNameDuplicate()
    {
        _sourceCache.Edit(list =>
        {
            var dictionary = list.Items
                .Where(o=>!string.IsNullOrWhiteSpace(o.Name))
                .GroupBy(o=>o.Name)
                .ToDictionary(o=>o.Key??string.Empty,o=>o.ToList());
            foreach (var dictionaryKey in dictionary.Keys)
            {
                bool isDuplicate = dictionary[dictionaryKey].Count > 1;
                foreach (var codeListDto in dictionary[dictionaryKey])
                {
                    if (codeListDto.IsNameDuplicate != isDuplicate)
                    {
                        codeListDto.IsNameDuplicate = isDuplicate;
                        ValidateCodeListDtoAsync(codeListDto,"Name").Await();
                        list.AddOrUpdate(codeListDto);
                    }
                }
            }
        });
    }

    private void UpdateDuplicate()
    {
        _sourceCache.Edit(list =>
        {
            var dictionary = list.Items
                .Where(o=>!string.IsNullOrWhiteSpace(o.UniqueId))
                .GroupBy(o=>o.UniqueId)
                .ToDictionary(o=>o.Key,o=>o.ToList());
            foreach (var dictionaryKey in dictionary.Keys)
            {
                bool isDuplicate = dictionary[dictionaryKey].Count > 1;
                foreach (var codeListDto in dictionary[dictionaryKey])
                {
                    if (codeListDto.IsDuplicate != isDuplicate)
                    {
                        codeListDto.IsDuplicate = isDuplicate;
                        ValidateCodeListDtoAsync(codeListDto,"UniqueId").Await();
                        list.AddOrUpdate(codeListDto);
                    }
                }
            }
        });
    }

    private static Func<CodeListDto, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return trade => true;
        return o => Contains(searchText, o.UniqueId)
                    || Contains(searchText, o.Name)
                    || Contains(searchText,o.Code)
                    || Contains(searchText,o.Type)
                    || Contains(searchText,o.Terminology)
                    || Contains(searchText,o.CommentUniqueId);
    }

    private static bool Contains(string? searchText, string? value)
    {
        return (!string.IsNullOrWhiteSpace(value) && value.Contains(searchText!, StringComparison.OrdinalIgnoreCase));
    }
    
    private async Task ValidateCodeListDtoAsync(CodeListDto codeListDto,string propertyName = "")
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            codeListDto.ClearErrors();
        }
        else
        {
            codeListDto.RemoveError(propertyName);
        }

        var result = await _validator.ValidateAsync(codeListDto, options =>
        {
            if (!string.IsNullOrWhiteSpace(propertyName))
                options.IncludeProperties([propertyName]);
        });
        foreach (var validationFailure in result.Errors)
        {
            codeListDto.SetError(validationFailure.PropertyName,
                new DataGridValidationResult(validationFailure.ErrorMessage,
                    validationFailure.Severity==Severity.Error?DataGridValidationSeverity.Error
                        :DataGridValidationSeverity.Warning));
        }
    }
    
    public async Task LoadCodeLists()
    {
        // ȡ�������ݵ� PropertyChanged ����
        foreach (var codeListDto in _sourceCache.Items)
        {
            codeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        }

        var list = await _codeListService.GetAllCodeListDtosAsync();
        foreach (var codeListDto in list)
        {
            await ValidateCodeListDtoAsync(codeListDto);
            codeListDto.PropertyChanged += CodeListDtoOnPropertyChanged;
        }
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
        UpdateDuplicate();
        UpdateNameDuplicate();
    }

    private void CodeListDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CodeListDto codeListDto) return;

        switch (e.PropertyName)
        {
            case nameof(CodeListDto.CommentUniqueId):
                HandleCommentUniqueIdChanged(codeListDto);
                break;
            case nameof(CodeListDto.UniqueId):
                UpdateDuplicate();
                break;
            case nameof(CodeListDto.Name):
                UpdateNameDuplicate();
                break;
        }

        if (e.PropertyName == nameof(CodeListDto.IsSelected)
            || e.PropertyName == nameof(CodeListDto.HasChanged)
            || e.PropertyName == nameof(CodeListDto.Comment)
            || e.PropertyName == nameof(CodeListDto.CommentId)) return;
        Observable.StartAsync(async () =>
        {
            await _validator.ValidateDtoAsync(codeListDto,e.PropertyName);
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
        // ValidateCodeListDtoAsync(codeListDto, "CommentUniqueId").Await();
        // _sourceCache.AddOrUpdate(codeListDto);
    }
    
    public async Task LoadComments()
    {
        var list = await _commentService.GetAllCommentsAsync();
        List<IAutoCompleteOption> res = [];
        foreach (var comment in list)
        {
            var autoCompleteOption = new AutoCompleteOption(){Header = comment.Description,Content = comment.UniqueId};
            res.add(autoCompleteOption);
        }
        Comments.Clear();
        Comments.AddRange(list);
        CommentOptions.Clear();
        CommentOptions.AddRange(res);
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
        if (result.Result != ButtonResult.OK
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
    private async Task DeleteAsync(CodeListDto codeList)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete CodeList" },
            { "Message", $"Are you sure you want to delete code list {codeList.Name}?" }
        });
        if (result.Result != ButtonResult.OK)
            return;

        await _codeListService.DeleteCodeListAsync(codeList);
        _sourceCache.Edit(o =>
        {
            o.Remove(codeList);
        });
        _messageService.Success("ɾ���ɹ�");
    }
    
    [RelayCommand]
    private async Task AddComment(CodeListDto codeList)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Comment" },
            { "ProjectId", _currentProjectService.CurrentProject!.Id },
            { "DefaultId",$"COM.CL.{codeList.UniqueId}"}
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog",dialogParameters);
        if (result.Parameters.TryGetValue<Comment>("Model",out Comment? comment))
        {
            Comment entity = await _commentService.InsertCommentAsync(comment);
            codeList.Comment = entity;
            codeList.CommentId = entity.Id;
            codeList.CommentUniqueId = entity.UniqueId;
            _sourceCache.Edit(o=>o.AddOrUpdate(codeList));
            var updateResult = await _codeListService.UpdateCodeListAsync(codeList);
            if(updateResult>0)
                _messageService.Success("Comment���ӳɹ�");
        }
    }
    
    [RelayCommand]
    private async Task ModifyComment(Comment comment)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "ProjectId", _currentProjectService.CurrentProject?.Id ?? 0 },
            { "Model", comment }
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog",dialogParameters);
        if (result.Parameters.TryGetValue<Comment>("Model",out Comment? resultModel))
        {
            await _commentService.UpdateCommentAsync(resultModel);
            _messageService.Success("Comment���³ɹ�");
        }
    }

    [RelayCommand]
    private async Task DeleteComment(Comment? comment)
    {
        var dictionary = await _commentService.ConfirmCommentRefenceAsync(comment);
        dictionary.TryGetValue("Datasets",out string? datasets);
        dictionary.TryGetValue("Variables",out string? variables);
        if (comment != null)
        {
            var dialogParameters = new DialogParameters
            {
                { "Title", comment.UniqueId??string.Empty },
                { "Variables", variables??string.Empty },
                { "Datasets",datasets??string.Empty}
            };
            var result = await _dialogHostService.ShowDialogAsync("DeleteCommentDialog",dialogParameters);
            if (result.Result == ButtonResult.OK)
            {
                await _commentService.DeleteCommentAsync(comment);
                var codeLists = CodeLists.Where(o=>o.CommentId == comment.Id).ToList();
                foreach (var codeList in codeLists)
                {
                    codeList.CommentId = 0;
                    codeList.CommentUniqueId = string.Empty;
                    codeList.Comment = null;
                }
                _sourceCache.Edit(o=>o.AddOrUpdate(codeLists));
                _messageService.Success("ɾ���ɹ�");
            }
        }
    }
    
    
    [RelayCommand]
    private async Task AddCodeList()
    {
        var dialogParameters = new DialogParameters
        {
            { "CdiscDataType", CdiscDataType }
        };
        var result = await _dialogHostService.ShowDialogAsync("AddCodeListDialog",dialogParameters);
        if (result.Parameters.TryGetValue<CodeList>("CodeList",out CodeList? codeList))
        {
            CodeListDto entity = await _codeListService.InsertCodeListAsync(codeList);
            await ValidateCodeListDtoAsync(entity);
            _sourceCache.Edit(o=>o.AddOrUpdate(entity));
            _messageService.Success("CodeList���ӳɹ�");
        }
    }
    
    [RelayCommand]
    private async Task Save()
    {
        await _codeListService.SaveCodeListsAsync(CodeLists.ToList());
        _messageService.Success("CodeList Save Success");
        HasChanges = false;
    }
    
    [RelayCommand]
    private async Task Discard()
    {
        if(!HasChanges) return;
        await LoadCodeLists();
        HasChanges = false;
        
    }
    
    
    public override void OnNavigatedTo(NavigationContext navigationContext)
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
    }


    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // ȡ������ CodeListDto �� PropertyChanged ����
        foreach (var codeListDto in _sourceCache.Items)
        {
            codeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        }

        if(!HasChanges) return;
        _codeListService.SaveCodeListsAsync(CodeLists.ToList()).Await();
        _messageService.Success("CodeList Save Success");
    }
    
}
