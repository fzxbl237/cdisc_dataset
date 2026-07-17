using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using AtomUI.Controls;
using cdisc_dataset.Constants;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using Prism.Dialogs;
using Prism.Navigation.Regions;
using ReactiveUI;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class VariablesViewModel : ConfirmNavigationViewModelBase
{
    private readonly IVariableService _variableService;
    private readonly IMethodService _methodService;
    private readonly ICommentService _commentService;
    private readonly ICodeListService _codeListService;
    private readonly IDictionaryService _dictionaryService;
    private readonly IMessageService _messageService;
    private readonly IDialogHostService _dialogHostService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMapper _mapper;
    private readonly IValidator<VariableDto> _validator;

    public AvaloniaList<string> Yns { get; set; } = ["Yes", "No"]; 
    public AvaloniaList<string> DataTypes { get; set; } = [.. ConstantOptions.DataTypes];
    
    public AvaloniaList<string> Origins { get; set; } = [];
    
    public AvaloniaList<string> Sources { get; set; } = ["", "Investigator", "Subject","Protocol","Vendor"];
    
    private readonly SourceCache<VariableDto, int> _sourceCache = new(o => o.Id);

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private CdiscDataType _cdiscDataType;
    [ObservableProperty] private string? _datasetFilter;
    [ObservableProperty] private string? _variableFilter;
    
    
    private FrozenDictionary<string, Method>? _frozenMethodDictionary;
    private FrozenDictionary<string, Comment>? _frozenCommentDictionary;
    private FrozenDictionary<string, CodeList>? _frozenCodeListDictionary;
    private FrozenDictionary<string, Dictionary>? _frozenDictionaryDictionary;
    
    public AvaloniaList<IAutoCompleteOption> MethodOptions { get; set; } = [];
    public AvaloniaList<IAutoCompleteOption> CommentOptions { get; set; } = [];
    public AvaloniaList<IAutoCompleteOption> CodeListOptions { get; set; } = [];

    private readonly ReadOnlyObservableCollection<VariableDto> _variables;
    
    private readonly CompositeDisposable _disposables = new();
    public ReadOnlyObservableCollection<VariableDto> Variables => _variables;

    public VariablesViewModel(
        IVariableService variableService,
        IMethodService methodService,
        ICommentService commentService,
        ICodeListService codeListService,
        IDictionaryService dictionaryService,
        IMessageService messageService,
        IDialogHostService dialogHostService,
        ICurrentProjectService currentProjectService,
        IMapper mapper,
        IValidator<VariableDto> validator)
    {
        _variableService = variableService;
        _methodService = methodService;
        _commentService = commentService;
        _codeListService = codeListService;
        _dictionaryService = dictionaryService;
        _messageService = messageService;
        _dialogHostService = dialogHostService;
        _currentProjectService = currentProjectService;
        _mapper = mapper;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);
        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _variables, SortExpressionComparer<VariableDto>.Ascending(o => o.DatasetName??string.Empty).ThenByAscending(o => o.Order))
            .Subscribe()
            .DisposeWith(_disposables);
        
        
        // _sourceCache
        //     .Connect()
        //     .WhenAnyPropertyChanged()
        //     .Subscribe(variableDto =>
        //         {
        //             
        //             Observable.StartAsync(async () =>
        //             {
        //                 if (variableDto != null)
        //                 {
        //                     await _validator.ValidateDtoAsync(variableDto);
        //                     _sourceCache.AddOrUpdate(variableDto);
        //                 }
        //             });
        //             variableDto?.HasChanged = true;
        //             HasChanges = true;
        //         })
        //     .DisposeWith(_disposables);
        

    }
    private static Func<VariableDto, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return _ => true;
        return o => Contains(searchText, o.DatasetName)
                    || Contains(searchText, o.VariableName)
                    || Contains(searchText, o.Label)
                    || Contains(searchText, o.DataType)
                    || Contains(searchText, o.Origin)
                    || Contains(searchText, o.Source)
                    || Contains(searchText, o.Core);
    }

    private static bool Contains(string? searchText, string? value)
    {
        return (!string.IsNullOrWhiteSpace(value) && value.Contains(searchText!, StringComparison.OrdinalIgnoreCase));
    }
    
    public async Task LoadVariablesAsync()
    {
        var sw = Stopwatch.StartNew();

        // // ȡ�������ݵ� PropertyChanged ����
        foreach (var variableDto in _sourceCache.Items)
        {
            variableDto.PropertyChanged -= VariableDtoOnPropertyChanged;
        }

        var list = await _variableService.GetAllVariableDtosAsync();
        foreach (var variableDto in list)
        {
           await _validator.ValidateDtoAsync(variableDto);
           variableDto.PropertyChanged+= VariableDtoOnPropertyChanged;
        }
        var swElapsedMilliseconds = sw.ElapsedMilliseconds;
        sw.Restart();
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
        var swElapsedMilliseconds2 = sw.ElapsedMilliseconds;
        sw.Restart();
        Console.WriteLine(swElapsedMilliseconds+" "+swElapsedMilliseconds2+" ");
        HasChanges = false;
    }

    private void VariableDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not VariableDto variableDto) return;

        switch (e.PropertyName)
        {
            case nameof(VariableDto.MethodUniqueId):
                HandleMethodUniqueIdChanged(variableDto);
                break;
            case nameof(VariableDto.CodeListUniqueId):
                HandleCodeListUniqueIdChanged(variableDto);
                break;
            case nameof(VariableDto.CommentUniqueId):
                HandleCommentUniqueIdChanged(variableDto);
                break;
        }

        if (e.PropertyName != nameof(VariableDto.HasChanged))
        {
            Observable.StartAsync(async () =>
            {
                await _validator.ValidateDtoAsync(variableDto,e.PropertyName);
                _sourceCache.AddOrUpdate(variableDto);
            });
            variableDto.HasChanged = true;
            HasChanges = true;
        }
        

    }

    private void HandleMethodUniqueIdChanged(VariableDto variableDto)
    {
        if (_frozenMethodDictionary != null && _frozenMethodDictionary.TryGetValue(variableDto.MethodUniqueId ?? string.Empty, out var method))
        {
            variableDto.Method = method;
            variableDto.MethodId = method.Id;
        }
        else
        {
            variableDto.Method = null;
            variableDto.MethodId = 0;
        }
        
    }

    private void HandleCodeListUniqueIdChanged(VariableDto variableDto)
    {
        if (_frozenDictionaryDictionary != null && _frozenDictionaryDictionary.TryGetValue(variableDto.CodeListUniqueId ?? string.Empty, out var dictionary))
        {
            variableDto.Dictionary = dictionary;
            variableDto.DictionaryId = dictionary.Id;
            variableDto.CodeList = null;
            variableDto.CodeListId = 0;
        }
        else if (_frozenCodeListDictionary != null && _frozenCodeListDictionary.TryGetValue(variableDto.CodeListUniqueId ?? string.Empty, out var codeList))
        {
            variableDto.CodeList = codeList;
            variableDto.CodeListId = codeList.Id;
            variableDto.Dictionary = null;
            variableDto.DictionaryId = 0;
        }
        else
        {
            variableDto.CodeList = null;
            variableDto.CodeListId = 0;
            variableDto.Dictionary = null;
            variableDto.DictionaryId = 0;
        }
    }

    private void HandleCommentUniqueIdChanged(VariableDto variableDto)
    {
        if (_frozenCommentDictionary != null && _frozenCommentDictionary.TryGetValue(variableDto.CommentUniqueId ?? string.Empty, out var comment))
        {
            variableDto.Comment = comment;
            variableDto.CommentId = comment.Id;
        }
        else
        {
            variableDto.Comment = null;
            variableDto.CommentId = 0;
        }
    }


    public async Task LoadLookups()
    {
        var methods = await _methodService.GetAllMethodsWithoutErorrAsync();
        _frozenMethodDictionary = methods
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
        
        MethodOptions.Clear();
        MethodOptions.AddRange(methods
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .Select(o => new VariableAutoCompleteOption
            {
                Header = $"{o.UniqueId} {o.Name}",
                Content = o.UniqueId,
                Method = o
            }));

        var comments = await _commentService.GetAllCommentsWithoutErorrAsync();
        
        _frozenCommentDictionary = comments
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
        
        CommentOptions.Clear();
        CommentOptions.AddRange(comments
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .Select(o => new VariableAutoCompleteOption
            {
                Header = $"{o.UniqueId} {o.Description}",
                Content = o.UniqueId,
                Comment = o
            }));

        var codeLists = await _codeListService.GetAllCodeListsWithoutErorrAsync();
        var dictionaries = await _dictionaryService.GetAllDictionariesWithoutErorrAsync();
        _frozenCodeListDictionary = codeLists
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
        _frozenDictionaryDictionary =  dictionaries
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
        
        CodeListOptions.Clear();
        CodeListOptions.AddRange(dictionaries
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .Select(o => new VariableAutoCompleteOption
            {
                Header = $"{o.UniqueId} {o.Name}",
                Content = o.UniqueId,
                UniqueId =  o.UniqueId,
                Name = o.Name,
                Color = "success",
                Tag = "Dictionary"
            }));
        
        CodeListOptions.AddRange(codeLists
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .Select(o => new VariableAutoCompleteOption
            {
                Header = $"{o.UniqueId} {o.Name}",
                Content = o.UniqueId,
                UniqueId =  o.UniqueId,
                Name = o.Name,
                Color = "warning",
                Tag = "CodeList"
            }));
    }
    
    [RelayCommand]
    private async Task DeleteAsync(VariableDto variable)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Variable" },
            { "Message", $"Are you sure you want to delete variable {variable.VariableName}?" }
        });
        if (result.Result != ButtonResult.OK)
            return;

        variable.PropertyChanged -= VariableDtoOnPropertyChanged;
        await _variableService.DeleteVariableAsync(variable);
        _sourceCache.Edit(o =>
        {
            o.Remove(variable);
        });
        _messageService.Success("ɾ���ɹ�");
    }
    
    [RelayCommand]
    private async Task AddVariable()
    {
        if (_currentProjectService.CurrentProject == null) return;

        var dialogParameters = new DialogParameters
        {
            { "DatasetName", DatasetFilter }
        };
        var result = await _dialogHostService.ShowDialogAsync("VariableDialog", dialogParameters);
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<VariableDto>>("Variables", out var variables) ||
            variables.Count == 0)
        {
            return;
        }
        await _variableService.SaveVariablesAsync(variables);
        await LoadVariablesAsync();
        _messageService.Success("Variable���ӳɹ�");
    }

    [RelayCommand]
    private async Task CreateCodeList(VariableDto? variable)
    {
        if (variable == null || _currentProjectService.CurrentProject == null)
            return;

        var dialogParameters = new DialogParameters
        {
            { "Variable", variable }
        };
        var result = await _dialogHostService.ShowDialogAsync("AddCodeListDialog", dialogParameters);
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<CodeList?>("CodeList", out var codeList) ||
            codeList == null)
        {
            return;
        }

        var entity = await _codeListService.InsertCodeListAsync(codeList);
        variable.CodeListId = entity.Id;
        variable.CodeListUniqueId = entity.UniqueId;
        variable.CodeList = _mapper.Map<CodeList>(entity);
        await _variableService.UpdateVariableAsync(variable);
        _sourceCache.Edit(o => o.AddOrUpdate(variable));
        _messageService.Success("CodeList created and linked to variable successfully");
    }
    
    [RelayCommand]
    private async Task Save()
    {
        if (!HasChanges) return;
        await _variableService.SaveVariablesAsync(_sourceCache.Items.Where(o=>o.HasChanged).ToList());
        //await _variableService.SaveVariablesAsync(_sourceCache.Items);
        HasChanges = false;
        _messageService.Success("Save successful");
        await LoadVariablesAsync();
    }
    
    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null) return;
        await LoadVariablesAsync();
    }
    
    [RelayCommand]
    private async Task AddComment(VariableDto variable)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Comment" },
            { "DefaultId",$"COM.{variable.VariableName}"}
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog",dialogParameters);
        if (result.Parameters.TryGetValue<CommentDto>("Model",out CommentDto? comment))
        {
            var commentDto = await _commentService.InsertCommentAsync(comment);
            var entity = _mapper.Map<Comment>(comment);
            variable.Comment = entity;
            variable.CommentId = entity.Id;
            variable.CommentUniqueId = entity.UniqueId;
            _sourceCache.Edit(o=>o.AddOrUpdate(variable));
            await _variableService.UpdateVariableAsync(variable);
            _messageService.Success("Comment���ӳɹ�");
        }
    }
    
    [RelayCommand]
    private async Task ModifyComment(VariableDto variable)
    {
        if(variable.Comment==null) return;
        var commentDto = _mapper.Map<CommentDto>(variable.Comment);
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "Model", commentDto }
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog",dialogParameters);
        if (result.Parameters.TryGetValue<CommentDto>("Model",out CommentDto? model))
        {
            var entity = await _commentService.UpdateCommentAsync(model);
            variable.Comment = entity;
            variable.CommentId = entity.Id;
            variable.CommentUniqueId = entity.UniqueId;
            _sourceCache.Edit(o=>o.AddOrUpdate(variable));
            await _variableService.UpdateVariableAsync(variable);
            _messageService.Success("Comment���³ɹ�");
        }
    }

    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        base.OnNavigatedFrom(navigationContext);

        //ȡ������ VariableDto �� PropertyChanged ����
        foreach (var variableDto in _sourceCache.Items)
        {
            variableDto.PropertyChanged -= VariableDtoOnPropertyChanged;
        }
        
        _disposables.Dispose();
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        if (cdiscDataType == CdiscDataType.Sdtm)
        {
            Origins.AddRange([
                "", "Collected", "Derived", "Assigned", "Protocol", "Predecessor"
            ]);
        }
        else
        {
            Origins.AddRange([
                "", "Derived", "Assigned", "Predecessor"
            ]);
        }

        CdiscDataType = cdiscDataType;
    }


    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }
    
}

public record VariableAutoCompleteOption : AutoCompleteOption
{
    public Method? Method { get; set; }
    public Comment? Comment { get; set; }
    public CodeList? CodeList { get; set; }
    
    public string? UniqueId { get; set; }
    
    public string? Name { get; set; }
    
    public string? Color { get; set; }
    
    public string? Tag { get; set; }
}
