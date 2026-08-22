using AsyncNavigation;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Avalonia.Threading;
using PatChes.Constants;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using NavigationContext = AsyncNavigation.NavigationContext;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.ViewModels.Defines;

public partial class VariablesViewModel : ConfirmNavigationViewModelBase
{
    private readonly IVariableService _variableService;
    private readonly ICommentService _commentService;
    private readonly IMethodService _methodService;
    private readonly IReferenceDeletionService _referenceDeletionService;
    private readonly ICodeListService _codeListService;
    private readonly IDictionaryService _dictionaryService;
    private readonly IMessageService _messageService;
    private readonly IDialogHostService _dialogHostService;
    private readonly PatChes.Services.IDialogService _dialogService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMapper _mapper;
    private readonly IValidator<VariableDto> _validator;

    public AvaloniaList<string> Yns { get; set; } = ["Yes", "No"]; 
    public AvaloniaList<string> DataTypes { get; set; } = [.. ConstantOptions.DataTypes];
    
    public AvaloniaList<string> Origins { get; set; } = [];
    
    public AvaloniaList<string> Sources { get; set; } = [..ConstantOptions.Sources];
    
    private readonly SourceCache<VariableDto, int> _sourceCache = new(o => o.Id);

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _isErrorOnly;
    [ObservableProperty] private bool _hasChanges;
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
        ICommentService commentService,
        IMethodService methodService,
        IReferenceDeletionService referenceDeletionService,
        ICodeListService codeListService,
        IDictionaryService dictionaryService,
        IMessageService messageService,
        IDialogHostService dialogHostService,
        PatChes.Services.IDialogService dialogService,
        ICurrentProjectService currentProjectService,
        IMapper mapper,
        IValidator<VariableDto> validator,
        ILookupStore lookupStore)
    {
        _variableService = variableService;
        _commentService = commentService;
        _methodService = methodService;
        _referenceDeletionService = referenceDeletionService;
        _codeListService = codeListService;
        _dictionaryService = dictionaryService;
        _messageService = messageService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _currentProjectService = currentProjectService;
        _mapper = mapper;
        _validator = validator;

        var filter = Observable.Merge(
                this.WhenValueChanged(t => t.SearchText)
                    .Throttle(TimeSpan.FromMilliseconds(250)),
                this.WhenValueChanged(t => t.IsErrorOnly)
                    .Select(_ => SearchText))
            .Select(_ => BuildFilter());
        _sourceCache.Connect()
            .AutoRefresh(o => o.HasErrors)
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _variables, SortExpressionComparer<VariableDto>.Ascending(o => o.DatasetName??string.Empty).ThenByAscending(o => o.Order))
            .Subscribe()
            .DisposeWith(_disposables);

        lookupStore.Methods
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildMethodLookups);

        lookupStore.Comments
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildCommentLookups);

        Observable.CombineLatest(
                lookupStore.CodeLists.ToCollection(),
                lookupStore.Dictionaries.ToCollection(),
                (codeLists, dictionaries) => (CodeLists: codeLists, Dictionaries: dictionaries))
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(x => RebuildCodeListAndDictionaryLookups(x.CodeLists, x.Dictionaries));
        
        
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
    partial void OnIsErrorOnlyChanged(bool value) => _sourceCache.Refresh();

    private Func<VariableDto, bool> BuildFilter()
    {
        var searchText = SearchText;
        if (string.IsNullOrEmpty(searchText)) return o => !IsErrorOnly || o.HasErrors;
        return o => (!IsErrorOnly || o.HasErrors) && (Contains(searchText, o.DatasetName)
                    || Contains(searchText, o.VariableName)
                    || Contains(searchText, o.Label)
                    || Contains(searchText, o.DataType)
                    || Contains(searchText, o.Origin)
                    || Contains(searchText, o.Source)
                    || Contains(searchText, o.Core));
    }

    private static bool Contains(string? searchText, string? value)
    {
        return (!string.IsNullOrWhiteSpace(value) && value.Contains(searchText!, StringComparison.OrdinalIgnoreCase));
    }
    
    public async Task LoadVariablesAsync()
    {
        foreach (var variableDto in _sourceCache.Items)
        {
            variableDto.PropertyChanged -= VariableDtoOnPropertyChanged;
        }

        var list = await _variableService.GetAllVariableDtosAsync();
        foreach (var variableDto in list)
        {
            await _validator.ValidateDtoAsync(variableDto);
            variableDto.PropertyChanged += VariableDtoOnPropertyChanged;
        }
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
        HasChanges = false;
    }

    private void VariableDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not VariableDto variableDto || string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName == nameof(VariableDto.HasChanged))
        {
            return;
        }

        if (e.PropertyName is not (
                nameof(VariableDto.Order) or
                nameof(VariableDto.DatasetName) or
                nameof(VariableDto.VariableName) or
                nameof(VariableDto.Label) or
                nameof(VariableDto.DataType) or
                nameof(VariableDto.Length) or
                nameof(VariableDto.SignificantDigits) or
                nameof(VariableDto.Format) or
                nameof(VariableDto.Mandatory) or
                nameof(VariableDto.CodeListUniqueId) or
                nameof(VariableDto.Origin) or
                nameof(VariableDto.Source) or
                nameof(VariableDto.Pages) or
                nameof(VariableDto.MethodUniqueId) or
                nameof(VariableDto.Predecessor) or
                nameof(VariableDto.Role) or
                nameof(VariableDto.HasNoData) or
                nameof(VariableDto.CommentUniqueId) or
                nameof(VariableDto.DeveloperNotes)))
        {
            return;
        }

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(VariableDto.VariableName):
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.VariableName), nameof(VariableDto.Origin), nameof(VariableDto.CodeListUniqueId));
                    break;
                case nameof(VariableDto.DataType):
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.DataType), nameof(VariableDto.Length), nameof(VariableDto.SignificantDigits));
                    break;
                case nameof(VariableDto.Origin):
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.Origin), nameof(VariableDto.MethodUniqueId), nameof(VariableDto.Predecessor), nameof(VariableDto.Pages));
                    break;
                case nameof(VariableDto.Source):
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.Source), nameof(VariableDto.Pages));
                    break;
                case nameof(VariableDto.MethodUniqueId):
                    ApplyMethod(variableDto, variableDto.MethodUniqueId);
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.MethodUniqueId));
                    break;
                case nameof(VariableDto.CodeListUniqueId):
                    ApplyCodeList(variableDto, variableDto.CodeListUniqueId);
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.CodeListUniqueId));
                    break;
                case nameof(VariableDto.CommentUniqueId):
                    ApplyComment(variableDto, variableDto.CommentUniqueId);
                    await _validator.ValidateDtoAsync(variableDto, nameof(VariableDto.CommentUniqueId));
                    break;
                default:
                    await _validator.ValidateDtoAsync(variableDto, e.PropertyName);
                    break;
            }
            _sourceCache.AddOrUpdate(variableDto);
        });

        variableDto.HasChanged = true;
        HasChanges = true;
    }

    private void ApplyMethod(VariableDto variableDto, string? methodUniqueId)
    {
        if (methodUniqueId == variableDto.Method?.UniqueId)
            return;

        if (string.IsNullOrWhiteSpace(methodUniqueId) || _frozenMethodDictionary == null ||
            !_frozenMethodDictionary.TryGetValue(methodUniqueId, out var method))
        {
            variableDto.Method = null;
            variableDto.MethodId = 0;
            return;
        }

        variableDto.Method = method;
        variableDto.MethodId = method.Id;
        variableDto.MethodUniqueId = method.UniqueId;
    }

    private void ApplyCodeList(VariableDto variableDto, string? codeListUniqueId)
    {
        if (codeListUniqueId == variableDto.CodeList?.UniqueId || codeListUniqueId == variableDto.Dictionary?.UniqueId)
            return;

        if (!string.IsNullOrWhiteSpace(codeListUniqueId) && _frozenDictionaryDictionary != null &&
            _frozenDictionaryDictionary.TryGetValue(codeListUniqueId, out var dictionary))
        {
            variableDto.Dictionary = dictionary;
            variableDto.DictionaryId = dictionary.Id;
            variableDto.DictionaryUniqueId = dictionary.UniqueId;
            variableDto.CodeList = null;
            variableDto.CodeListId = 0;
            variableDto.CodeListUniqueId = dictionary.UniqueId;
            return;
        }

        if (!string.IsNullOrWhiteSpace(codeListUniqueId) && _frozenCodeListDictionary != null &&
            _frozenCodeListDictionary.TryGetValue(codeListUniqueId, out var codeList))
        {
            variableDto.CodeList = codeList;
            variableDto.CodeListId = codeList.Id;
            variableDto.CodeListUniqueId = codeList.UniqueId;
            variableDto.Dictionary = null;
            variableDto.DictionaryId = 0;
            variableDto.DictionaryUniqueId = string.Empty;
            return;
        }

        variableDto.CodeList = null;
        variableDto.CodeListId = 0;
        variableDto.Dictionary = null;
        variableDto.DictionaryId = 0;
        variableDto.DictionaryUniqueId = string.Empty;
    }

    private void ApplyComment(VariableDto variableDto, string? commentUniqueId)
    {
        if (commentUniqueId == variableDto.Comment?.UniqueId)
            return;

        if (string.IsNullOrWhiteSpace(commentUniqueId) || _frozenCommentDictionary == null ||
            !_frozenCommentDictionary.TryGetValue(commentUniqueId, out var comment))
        {
            variableDto.Comment = null;
            variableDto.CommentId = 0;
            return;
        }

        variableDto.Comment = comment;
        variableDto.CommentId = comment.Id;
        variableDto.CommentUniqueId = comment.UniqueId;
    }


    private void RebuildMethodLookups(IReadOnlyCollection<Method> methods)
    {
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
    }

    private void RebuildCommentLookups(IReadOnlyCollection<Comment> comments)
    {
        var validComments = comments
            .Where(o => !o.HasErrors && !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToList();

        _frozenCommentDictionary = validComments
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        CommentOptions.Clear();
        CommentOptions.AddRange(validComments.Select(o => new VariableAutoCompleteOption
        {
            Header = $"{o.UniqueId} {o.Description}",
            Content = o.UniqueId,
            Comment = o
        }));
    }

    private void RebuildCodeListAndDictionaryLookups(
        IReadOnlyCollection<CodeList> codeLists,
        IReadOnlyCollection<Dictionary> dictionaries)
    {
        _frozenCodeListDictionary = codeLists
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
        _frozenDictionaryDictionary = dictionaries
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        CodeListOptions.Clear();
        CodeListOptions.AddRange(dictionaries
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .Select(o => new VariableAutoCompleteOption
            {
                Header = $"{o.UniqueId} {o.Name}",
                Content = o.UniqueId,
                UniqueId = o.UniqueId,
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
                UniqueId = o.UniqueId,
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
        if (result.Result != DialogButtonResult.OK)
            return;

        variable.PropertyChanged -= VariableDtoOnPropertyChanged;
        await _variableService.DeleteVariableAsync(variable);
        _sourceCache.Edit(o =>
        {
            o.Remove(variable);
        });
        _messageService.Success("Variable deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedVariables = _sourceCache.Items.Where(o => o.IsSelected).ToList();
        if (selectedVariables.Count == 0)
        {
            _messageService.Info("Please select at least one variable to delete.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Variables" },
            { "Message", $"Are you sure you want to delete {selectedVariables.Count} selected variable(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        foreach (var variable in selectedVariables)
        {
            variable.PropertyChanged -= VariableDtoOnPropertyChanged;
            await _variableService.DeleteVariableAsync(variable);
        }

        _sourceCache.Remove(selectedVariables);
        _messageService.Success($"{selectedVariables.Count} variable(s) deleted successfully.");
    }
    
    [RelayCommand]
    private async Task AddVariable()
    {
        if (_currentProjectService.CurrentProject == null) return;

        var result = await _dialogHostService.ShowDialogAsync("ImportSettingVariablesDialog", null);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<int>>("TemplateVariableIds", out var templateVariableIds))
        {
            return;
        }

        var importedCount = await _variableService.ImportSettingVariablesAsync(templateVariableIds);
        if (importedCount == 0)
        {
            _messageService.Info("No selected variables are available for import.");
            return;
        }

        await LoadVariablesAsync();
        _messageService.Success($"{importedCount} variable(s) imported from settings successfully.");
    }

    [RelayCommand]
    private async Task AddCodeListAsync(VariableDto variable)
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("CodeListDialog", new DialogParameters
        {
            { "Variable", variable }
        });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CodeList>("CodeList", out var codeList))
            return;

        var entity = await _codeListService.InsertCodeListAsync(codeList);
        if (!result.Parameters.TryGetValue<VariableDto>("Variable", out var selectedVariable) || selectedVariable == null)
            return;
        await LinkCodeListAsync(selectedVariable, entity);
        _messageService.Success("Code list created and linked to the variable successfully.");
    }

    [RelayCommand]
    private async Task EditCodeListAsync(VariableDto variable)
    {
        if (variable.CodeList == null)
            return;

        var codeListDto = (await _codeListService.GetAllCodeListDtosAsync())
            .FirstOrDefault(o => o.Id == variable.CodeList.Id);
        if (codeListDto == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("CodeListDialog", new DialogParameters
        {
            { "Model", codeListDto }
        });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CodeList>("CodeList", out var codeList))
            return;

        codeListDto = _mapper.Map<CodeListDto>(codeList);
        await _codeListService.UpdateCodeListWithTermsAsync(codeListDto);
        var updatedCodeList = _mapper.Map<CodeList>(codeListDto);
        foreach (var item in _sourceCache.Items.Where(o => o.CodeListId == codeListDto.Id).ToList())
        {
            item.CodeList = updatedCodeList;
            item.CodeListId = codeListDto.Id;
            item.CodeListUniqueId = codeListDto.UniqueId;
            await _variableService.UpdateVariableAsync(item);
            _sourceCache.AddOrUpdate(item);
        }
        _messageService.Success("Code list updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteCodeListAsync(VariableDto variable)
    {
        if (variable.CodeList == null)
            return;

        var codeList = variable.CodeList;
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Code List" },
            { "Message", $"Are you sure you want to delete code list {codeList.UniqueId}? All references will be cleared." }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        await _codeListService.DeleteCodeListAsync(_mapper.Map<CodeListDto>(codeList));
        var affectedVariables = _sourceCache.Items.Where(o => o.CodeListId == codeList.Id).ToList();
        foreach (var item in affectedVariables)
        {
            item.CodeList = null;
            item.CodeListId = 0;
            item.CodeListUniqueId = string.Empty;
            await _variableService.UpdateVariableAsync(item);
            _sourceCache.AddOrUpdate(item);
        }
        _messageService.Success("Code list deleted successfully.");
    }

    [RelayCommand]
    private async Task AddDictionaryAsync(VariableDto variable)
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogService.ShowAddDictionaryModelAsync();
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<DictionaryDto>("Model", out var dictionary))
            return;

        var entity = await _dictionaryService.InsertDictionaryAsync(dictionary);
        await LinkDictionaryAsync(variable, entity);
        _messageService.Success("Dictionary created and linked to the variable successfully.");
    }

    [RelayCommand]
    private async Task EditDictionaryAsync(VariableDto variable)
    {
        if (variable.Dictionary == null)
            return;

        var result = await _dialogService.ShowEditDictionaryModelAsync(_mapper.Map<DictionaryDto>(variable.Dictionary));
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<DictionaryDto>("Model", out var dictionary))
            return;

        var entity = await _dictionaryService.UpdateDictionaryAsync(dictionary);
        foreach (var item in _sourceCache.Items.Where(o => o.DictionaryId == dictionary.Id).ToList())
        {
            item.Dictionary = entity;
            item.DictionaryId = entity.Id;
            item.DictionaryUniqueId = entity.UniqueId;
            item.CodeListUniqueId = entity.UniqueId;
            await _variableService.UpdateVariableAsync(item);
            _sourceCache.AddOrUpdate(item);
        }
        _messageService.Success("Dictionary updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteDictionaryAsync(VariableDto variable)
    {
        if (variable.Dictionary == null)
            return;

        var dictionary = variable.Dictionary;
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Dictionary" },
            { "Message", $"Are you sure you want to delete dictionary {dictionary.UniqueId}? All references will be cleared." }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        await _dictionaryService.DeleteDictionaryAsync(_mapper.Map<DictionaryDto>(dictionary));
        var affectedVariables = _sourceCache.Items.Where(o => o.DictionaryId == dictionary.Id).ToList();
        foreach (var item in affectedVariables)
        {
            item.Dictionary = null;
            item.DictionaryId = 0;
            item.DictionaryUniqueId = string.Empty;
            item.CodeListUniqueId = string.Empty;
            await _variableService.UpdateVariableAsync(item);
            _sourceCache.AddOrUpdate(item);
        }
        _messageService.Success("Dictionary deleted successfully.");
    }

    private async Task LinkCodeListAsync(VariableDto variable, CodeListDto codeList)
    {
        variable.CodeList = _mapper.Map<CodeList>(codeList);
        variable.CodeListId = codeList.Id;
        variable.CodeListUniqueId = codeList.UniqueId;
        variable.Dictionary = null;
        variable.DictionaryId = 0;
        variable.DictionaryUniqueId = string.Empty;
        await _variableService.UpdateVariableAsync(variable);
        _sourceCache.AddOrUpdate(variable);
    }

    private async Task LinkDictionaryAsync(VariableDto variable, Dictionary dictionary)
    {
        variable.Dictionary = dictionary;
        variable.DictionaryId = dictionary.Id;
        variable.DictionaryUniqueId = dictionary.UniqueId;
        variable.CodeList = null;
        variable.CodeListId = 0;
        variable.CodeListUniqueId = dictionary.UniqueId;
        await _variableService.UpdateVariableAsync(variable);
        _sourceCache.AddOrUpdate(variable);
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
        var result = await _dialogHostService.ShowDialogAsync("CodeListDialog", dialogParameters);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CodeList?>("CodeList", out var codeList) ||
            codeList == null)
        {
            return;
        }

        var entity = await _codeListService.InsertCodeListAsync(codeList);
        if (!result.Parameters.TryGetValue<VariableDto>("Variable", out var selectedVariable) || selectedVariable == null)
            return;

        selectedVariable.CodeListId = entity.Id;
        selectedVariable.CodeListUniqueId = entity.UniqueId;
        selectedVariable.CodeList = _mapper.Map<CodeList>(entity);
        selectedVariable.Dictionary = null;
        selectedVariable.DictionaryId = 0;
        selectedVariable.DictionaryUniqueId = string.Empty;
        await _variableService.UpdateVariableAsync(selectedVariable);
        _sourceCache.Edit(o => o.AddOrUpdate(selectedVariable));
        _messageService.Success("Code list created and linked to the variable successfully.");
    }
    
    [RelayCommand]
    private async Task Save()
    {
        if (!HasChanges) return;
        await _variableService.SaveVariablesAsync(_sourceCache.Items.Where(o=>o.HasChanged).ToList());
        //await _variableService.SaveVariablesAsync(_sourceCache.Items);
        HasChanges = false;
        _messageService.Success("Variables saved successfully.");
        await LoadVariablesAsync();
    }
    
    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null) return;
        await LoadVariablesAsync();
    }
    
    [RelayCommand]
    private async Task AddCommentAsync(VariableDto variable)
    {
        var result = await _dialogService.ShowAddCommentModelAsync($"COM.{variable.VariableName}");
        if (result.Result == DialogButtonResult.Yes &&
            result.Parameters.TryGetValue<CommentDto>("Model", out var comment))
        {
            var commentDto = await _commentService.InsertCommentAsync(comment);
            var entity = _mapper.Map<Comment>(comment);
            variable.Comment = entity;
            variable.CommentId = entity.Id;
            variable.CommentUniqueId = entity.UniqueId;
            _sourceCache.Edit(o=>o.AddOrUpdate(variable));
            await _variableService.UpdateVariableAsync(variable);
            _messageService.Success("Comment added successfully.");
        }
    }
    
    [RelayCommand]
    private async Task EditCommentAsync(VariableDto variable)
    {
        if(variable.Comment==null) return;
        var commentDto = _mapper.Map<CommentDto>(variable.Comment);
        var result = await _dialogService.ShowEditCommentModelAsync(commentDto);
        if (result.Result == DialogButtonResult.Yes &&
            result.Parameters.TryGetValue<CommentDto>("Model", out var model))
        {
            var entity = await _commentService.UpdateCommentAsync(model);
            variable.Comment = entity;
            variable.CommentId = entity.Id;
            variable.CommentUniqueId = entity.UniqueId;
            _sourceCache.Edit(o=>o.AddOrUpdate(variable));
            await _variableService.UpdateVariableAsync(variable);
            _messageService.Success("Comment updated successfully.");
        }
    }

    [RelayCommand]
    private async Task LinkVariablesAsync(VariableDto variable)
    {
        if (variable.MethodId == 0 || string.IsNullOrWhiteSpace(variable.MethodUniqueId))
        {
            _messageService.Error("Please save the method before assigning variables.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("AssignVariablesDialog", new DialogParameters());
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<int>>("VariableIds", out var variableIds))
            return;

        var assignedCount = await _variableService.AssignMethodToVariablesAsync(
            variable.MethodId,
            variable.MethodUniqueId,
            variableIds);
        _messageService.Success($"Assigned method to {assignedCount} variable(s).");
    }

    [RelayCommand]
    private async Task DeleteCommentAsync(VariableDto variable)
    {
        if (variable.Comment == null || !await _referenceDeletionService.ConfirmAndDeleteCommentAsync(variable.Comment))
            return;

        var affectedVariables = _sourceCache.Items
            .Where(item => item.CommentId == variable.Comment.Id)
            .ToList();
        foreach (var affectedVariable in affectedVariables)
        {
            affectedVariable.Comment = null;
            affectedVariable.CommentId = 0;
            affectedVariable.CommentUniqueId = string.Empty;
        }
        _sourceCache.Edit(cache => cache.AddOrUpdate(affectedVariables));
        _messageService.Success("Comment deleted successfully.");
    }

    [RelayCommand]
    private async Task AddMethodAsync(VariableDto variable)
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogService.ShowAddMethodModelAsync(new MethodDto
        {
            ProjectId = _currentProjectService.CurrentProject.Id,
            CdiscDataType = _currentProjectService.CdiscDataType,
            UniqueId = $"{variable.DatasetName}.{variable.VariableName}",
            Name = $"Algorithm to derive {variable.DatasetName}.{variable.VariableName}",
            Type = "Computation"
        });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<MethodDto>("Model", out var method))
        {
            return;
        }

        var methodDto = await _methodService.InsertMethodAsync(_mapper.Map<Method>(method));
        var methodEntity = _mapper.Map<Method>(methodDto);
        variable.Method = methodEntity;
        variable.MethodId = methodDto.Id;
        variable.MethodUniqueId = methodDto.UniqueId;
        _sourceCache.Edit(o => o.AddOrUpdate(variable));
        await _variableService.UpdateVariableAsync(variable);

        _messageService.Success("Method added and linked to the variable successfully.");
    }

    [RelayCommand]
    private async Task EditMethodAsync(VariableDto variable)
    {
        if (variable.Method == null)
            return;

        var result = await _dialogService.ShowEditMethodModelAsync(_mapper.Map<MethodDto>(variable.Method));
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<MethodDto>("Model", out var method))
        {
            return;
        }

        await _methodService.UpdateMethodAsync(method);
        variable.Method = _mapper.Map<Method>(method);
        variable.MethodId = method.Id;
        variable.MethodUniqueId = method.UniqueId;
        _sourceCache.Edit(o => o.AddOrUpdate(variable));
        await _variableService.UpdateVariableAsync(variable);
        _messageService.Success("Method updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteMethodAsync(VariableDto variable)
    {
        if (variable.Method == null || !await _referenceDeletionService.ConfirmAndDeleteMethodAsync(variable.Method))
            return;

        var affectedVariables = _sourceCache.Items
            .Where(item => item.MethodId == variable.Method.Id)
            .ToList();
        foreach (var affectedVariable in affectedVariables)
        {
            affectedVariable.Method = null;
            affectedVariable.MethodId = 0;
            affectedVariable.MethodUniqueId = string.Empty;
        }
        _sourceCache.Edit(cache => cache.AddOrUpdate(affectedVariables));
        _messageService.Success("Method deleted successfully.");
    }

    public override async Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        await base.OnNavigatedFromAsync(navigationContext);
        
        foreach (var variableDto in _sourceCache.Items)
        {
            variableDto.PropertyChanged -= VariableDtoOnPropertyChanged;
        }
        _sourceCache.Clear();
    }

    public override async Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        await base.OnNavigatedToAsync(navigationContext);
        var cdiscDataType = _currentProjectService.CdiscDataType;
        Origins.Clear();
        Origins.AddRange(cdiscDataType == CdiscDataType.Sdtm ? [.. ConstantOptions.SdtmOrigins] : [.. ConstantOptions.AdamOrigins]);

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
