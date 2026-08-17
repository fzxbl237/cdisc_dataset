using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Threading;
using cdisc_dataset.Controls.DataGrid;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Dm.util;
using DynamicData;
using DynamicData.Binding;
using LiteDB;
using MapsterMapper;
using P21.Validator.Api.Options;
using P21.Validator.Data;
using MiniExcelLibs;
using Prism.Dialogs;
using ReactiveUI;
using ReactiveUI.Builder;
using SqlSugar;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class CodeListDialogViewModel: ObservableObject, IDialogHostAware
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly ICommentService _commentService;
    private readonly IVariableService _variableService;
    private readonly ICodeListService _codeListService;
    private readonly IMessageService _messageService;
    private readonly ILiteDatabase _liteDatabase;
    private readonly ILiteCollection<ProjectFile> _projectFiles;
    private readonly IMapper _mapper;
    public string? DialogHostName { get; set; } = "Root";
    
    //public AvaloniaList<Comment> Comments { get; set; } = [];
    
    public AvaloniaList<string> Types { get; set; } = ["text", "integer", "float","datetime","date","time",
        "partialDate","partialTime","partialDateTime","incompleteDatetime","durationDatetime","intervalDatetime"];

    public AvaloniaList<string?> Terminologies  { get; set; } = [];
    
    [ObservableProperty]
    private AvaloniaList<CodeListOption> _codeLists = [];
    
    [ObservableProperty] private CodeListOption? _selectedCodeList;
    
    [ObservableProperty]
    private AvaloniaList<ISelectOption> _comments = [];
    
    [ObservableProperty]
    private ISelectOption? _selectedComment;
    
    [ObservableProperty]
    private VariableDto? _defaultVariable;

    public AvaloniaList<VariableOption> Variables { get; } = [];

    [ObservableProperty] private VariableOption? _selectedVariable;
    [ObservableProperty] private string _selectedVariableDisplay = string.Empty;
    [ObservableProperty] private bool _isVariableSelectionVisible;

    [ObservableProperty] private CodeListDto _codeListDto = new();

    [ObservableProperty] private bool _isInEditMode;
    
    private readonly SourceCache<TermDto,string> _sourceList= new (o=>o.Uuid);
    
    [ObservableProperty] private string? _searchText;
    
    [ObservableProperty] private string? _display;
    
    private readonly ReadOnlyObservableCollection<TermDto> _terms;
    public ReadOnlyObservableCollection<TermDto> Terms => _terms;
    

    [ObservableProperty] private TermOptionsAsyncLoader _termOptionsAsyncLoader;
    
    private readonly CompositeDisposable _disposables = new();
    
    public CodeListDialogViewModel(
        ISqlSugarClient sqlSugar,
        ICurrentProjectService currentProjectService,
        ICommentService commentService,
        IVariableService variableService,
        ICodeListService codeListService,
        IMessageService messageService,
        ILiteDatabase liteDatabase,
        IMapper mapper)
    {
        _sqlSugar = sqlSugar;
        _currentProjectService = currentProjectService;
        _commentService = commentService;
        _variableService = variableService;
        _codeListService = codeListService;
        _messageService = messageService;
        _liteDatabase = liteDatabase;
        _projectFiles = _liteDatabase.GetCollection<ProjectFile>("project_files");
        _mapper = mapper;
        TermOptionsAsyncLoader = new TermOptionsAsyncLoader(_sqlSugar);
        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);

        var observableCache = _sourceList.Connect().AsObservableCache();

        observableCache.Connect()
            .DisposeMany()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _terms,SortExpressionComparer<TermDto>.Ascending(o => o.Order))
            .Subscribe();
        
        _sourceList.Connect()
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            AttachTerm(change.Current);
                            break;
                        case ChangeReason.Remove:
                            DetachTerm(change.Current);
                            break;
                    }
                }

                if (changes.Any(change => change.Reason == ChangeReason.Remove))
                    UpdateTermsDuplicate();
            })
            .DisposeWith(_disposables);
    }

    private void AttachTerm(TermDto term)
    {
        term.PropertyChanged += TermOnPropertyChanged;
    }

    private void DetachTerm(TermDto term)
    {
        term.PropertyChanged -= TermOnPropertyChanged;
    }

    private void TermOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TermDto term || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        switch (e.PropertyName)
        {
            case nameof(TermDto.Name):
                var codeListRef = TermOptionsAsyncLoader.CodeListReference?.CodeListRef;
                var codeListTerm = string.IsNullOrWhiteSpace(codeListRef)
                    ? null
                    : _sqlSugar.AsTenant().QueryableWithAttr<CodeListTerm>()
                        .AsWithAttr()
                        .Where(o => o.CodeListRef == codeListRef && o.CodeValue == term.Name)
                        .First();
                term.Code = codeListTerm?.Code ?? string.Empty;
                term.DecodedValue = codeListTerm?.DecodedValue ?? string.Empty;

                UpdateTermsDuplicate();
                _sourceList.AddOrUpdate(term);
                break;
            case nameof(TermDto.IsNameDuplicate):
                _sourceList.AddOrUpdate(term);
                break;
            case nameof(TermDto.Order):
                UpdateTermsOrder();
                break;
            case nameof(TermDto.DecodedValue):
                UpdateDecodedValueConsistent();
                break;
        }
    }

    private void CodeListDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CodeListDto.Terminology) && string.IsNullOrWhiteSpace(CodeListDto.Terminology))
        {
            CodeLists.Clear();
            SelectedCodeList = null;
        }
    }

    private static Func<TermDto, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return trade => true;
        return o => Contains(searchText, o.Order.ToString(CultureInfo.InvariantCulture))
                    ||Contains(searchText, o.CodeListUniqueId)
                    || Contains(searchText, o.Name)
                    || Contains(searchText,o.Code)
                    || Contains(searchText,o.DecodedValue)
                    || Contains(searchText,o.CommentUniqueId);
    }
    
    private static bool Contains(string? searchText, string? value)
    {
        return (!string.IsNullOrWhiteSpace(value) && value.Contains(searchText!, StringComparison.OrdinalIgnoreCase));
    }
    
    public void OnDialogOpened(IDialogParameters parameters)
    {
        CodeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        DefaultVariable = null;
        SelectedVariable = null;
        Display = "Select CodeList";
        IsInEditMode = parameters.TryGetValue<CodeListDto>("Model", out var model);
        IsVariableSelectionVisible = !IsInEditMode &&
                                     (parameters.ContainsKey("Variable") ||
                                      (parameters.TryGetValue<bool>("SelectVariable", out var selectVariable) && selectVariable));
        if (IsInEditMode && model != null)
        {
            CodeListDto = model;
            _sourceList.Edit(items =>
            {
                items.Clear();
                items.AddOrUpdate(_mapper.Map<List<TermDto>>(model.Terms ?? []));
            });
            Display = $"Edit codelist: {model.UniqueId}";
        }
        else if (IsVariableSelectionVisible)
        {
            parameters.TryGetValue<VariableDto>("Variable", out var defaultVariable);
            LoadVariablesAsync(defaultVariable).AwaitWithOpt();
        }

        Terminologies.Clear();
        var list = _sqlSugar.Queryable<CodeListStd>().Select(o => o.Terminology).Distinct().ToList();
        Terminologies.Add(" ");
        Terminologies.AddRange(list);
        LoadComments().AwaitWithOpt();

        if (!IsInEditMode && Terminologies.Count >= 2)
        {
            CodeListDto.Terminology = Terminologies[1];
        }

        CodeListDto.PropertyChanged += CodeListDtoOnPropertyChanged;
        if (string.IsNullOrWhiteSpace(CodeListDto.Type))
            CodeListDto.Type = Types[0];
    }

    private async Task LoadVariablesAsync(VariableDto? defaultVariable)
    {
        var variables = await _variableService.GetAllVariableDtosAsync();
        Variables.Clear();
        Variables.AddRange(variables
            .OrderBy(o => o.DatasetName)
            .ThenBy(o => o.VariableName)
            .Select(o => new VariableOption
            {
                Header = $"{o.DatasetName}.{o.VariableName}",
                Content = $"{o.DatasetName}.{o.VariableName} {o.Label}",
                Variable = o
            }));

        if (defaultVariable == null)
            return;

        SelectedVariable = Variables.FirstOrDefault(o => o.Variable.Id == defaultVariable.Id);
    }

    partial void OnSelectedVariableChanged(VariableOption? value)
    {
        var variable = value?.Variable;
        DefaultVariable = variable;
        SelectedCodeList = null;
        CodeLists.Clear();
        _sourceList.Clear();

        if (variable == null)
        {
            SelectedVariableDisplay = string.Empty;
            Display = "Select CodeList";
            return;
        }

        SelectedVariableDisplay = $"{variable.DatasetName}.{variable.VariableName}";
        Display = $"Create codelist for variable: {SelectedVariableDisplay}";
        CodeListDto.Type = variable.DataType ?? Types[0];
        LoadTermsFromXptAsync().AwaitWithOpt();
    }

    partial void OnSelectedCodeListChanged(CodeListOption? value)
    {
        
        var codeListReference = value?.CodeListReference;
        CodeListDto.Code = codeListReference?.CodeListCode;
        CodeListDto.Name = codeListReference?.CodeListName;
        CodeListDto.UniqueId = codeListReference?.CodeListRef?.Split(".").LastOrDefault();
        TermOptionsAsyncLoader.CodeListReference = codeListReference;

        _sourceList.Edit((changes) =>
        {
            foreach (var changesItem in changes.Items)
            {
                changesItem.CodeListUniqueId = CodeListDto.UniqueId;
                changesItem.CodeList = _mapper.Map<CodeList>(CodeListDto);
                changes.AddOrUpdate(changesItem);
            }
        });
    }

    partial void OnSelectedCommentChanged(ISelectOption? value)
    {
        if (value == null)
        {
            CodeListDto.CommentId = 0;
            CodeListDto.Comment = null;
            CodeListDto.CommentUniqueId = string.Empty;
            return;
        }
        if (value.Content is not Comment comment) return;
        CodeListDto.CommentId =  comment.Id;
        CodeListDto.Comment =  comment;
        CodeListDto.CommentUniqueId =  comment.UniqueId;
    }
    
    private async Task LoadComments()
    {
        if(_currentProjectService.CurrentProject==null) return;
        var comments =  await _commentService.GetAllCommentsAsync();
        List<ISelectOption> res = [];
        foreach (var comment in comments)
        {
            if(string.IsNullOrWhiteSpace(comment.UniqueId) || string.IsNullOrWhiteSpace(comment.Description))
                continue;
            var selectOption = new SelectOption() { Header = comment.UniqueId,Content = comment };
            res.Add(selectOption);
        }
        Comments.Clear();
        Comments.AddRange(res);
    }
    

    [RelayCommand]
    private async Task LoadTermsFromXptAsync()
    {
        if (DefaultVariable == null)
        {
            _messageService.Error("current variable is null");
            return;
        }
        
        var datasetName = DefaultVariable.DatasetName;
        var variableName = DefaultVariable.VariableName;
        if (string.IsNullOrWhiteSpace(datasetName) || string.IsNullOrWhiteSpace(variableName))
            return;

        var currentProjectId = _currentProjectService.CurrentProject?.Id??0;

        var normalizedDatasetName = Path.GetFileNameWithoutExtension(datasetName.Trim());
        var projectFile = _projectFiles.Query()
            .Where(o => o.ProjectId == currentProjectId && o.FileType == ProjectFileType.Sdtm)
            .ToList()
            .FirstOrDefault(o => string.Equals(
                Path.GetFileNameWithoutExtension(o.FileName.Trim()),
                normalizedDatasetName,
                StringComparison.OrdinalIgnoreCase));
        if (projectFile == null)
        {
            _messageService.Error($"SDTM XPT file for {normalizedDatasetName} was not found in the current project. Please upload {normalizedDatasetName}.xpt first.");
            return;
        }

        if (!_liteDatabase.FileStorage.Exists(projectFile.StorageId.ToString()))
        {
            _messageService.Error($"SDTM XPT file {projectFile.FileName} is registered but its stored content is missing. Please upload it again.");
            return;
        }

        var values = await Task.Run(() => ReadDistinctValues(projectFile, variableName));
        if (values.Count == 0)
        {
            _messageService.Error($"No non-empty values were found for {DefaultVariable.DatasetName}");
            return;
        }

        var codeListRef = await _codeListService.GetCodeListRefByVariableAsync(variableName.ToUpperInvariant());

        if (codeListRef != null && !string.IsNullOrWhiteSpace(codeListRef.CodeListRef))
        {
            var codeListReference = await _codeListService.GetCodeListReferenceByOidAsync(codeListRef.CodeListRef);
            if (codeListReference != null)
            {
                CodeLists.Clear();
                var codeListOption = CreateCodeListOption(codeListReference);
                CodeLists.Add(codeListOption);
                SelectedCodeList = codeListOption;
            }
        }
        else
        {
            var codeListReferences = await _codeListService.GetAllCodeListReferencesAsync();
            CodeLists.Clear();
            CodeLists.AddRange(codeListReferences.Select(CreateCodeListOption));
            CodeListDto.Code = string.Empty;
            CodeListDto.Name =  DefaultVariable?.Label;
            CodeListDto.UniqueId = DefaultVariable?.VariableName;
        }
        var standardTerms = string.IsNullOrWhiteSpace(codeListRef?.CodeListRef)
            ? []
            : await _codeListService.GetCodeListTermsAsync(codeListRef.CodeListRef);
        var standardTermsByValue = standardTerms
            .Where(o => !string.IsNullOrWhiteSpace(o.CodeValue))
            .GroupBy(o => o.CodeValue!, StringComparer.Ordinal)
            .ToDictionary(o => o.Key, o => o.First(), StringComparer.Ordinal);

        _sourceList.Edit(updater =>
        {
            updater.Clear();
            var order = 1;
            foreach (var value in values)
            {
                standardTermsByValue.TryGetValue(value, out var standardTerm);
                updater.AddOrUpdate(new TermDto
                {
                    Order = order++,
                    Name = value,
                    Code = standardTerm?.Code,
                    DecodedValue = standardTerm?.DecodedValue,
                    CodeListUniqueId = CodeListDto.UniqueId
                });
            }
        });
        UpdateTermsDuplicate();
    }

    private List<string> ReadDistinctValues(ProjectFile projectFile, string variableName)
    {
        var storedFile = _liteDatabase.FileStorage.FindById(projectFile.StorageId.ToString());
        if (storedFile == null)
            return [];

        using var memoryStream = new MemoryStream();
        storedFile.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var validationOptions = ValidationOptions.CreateBuilder().Build();
        var factory = new DataEntryFactory(validationOptions);
        var options = SourceOptions.builder()
            .WithName(Path.GetFileNameWithoutExtension(projectFile.FileName).ToUpperInvariant())
            .WithMemoryStream(memoryStream)
            .WithType(SourceOptions.StandardTypes.SasTransport)
            .Build();

        using var dataSource = new SasTransportDataSource(options, factory);
        var sourceVariableName = dataSource.GetVariables()
            .FirstOrDefault(o => string.Equals(o, variableName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(sourceVariableName))
            return [];

        var values = new HashSet<string>(StringComparer.Ordinal);
        while (dataSource.HasRecords())
        {
            var records = dataSource.GetRecords();
            if (records.Count == 0)
                break;

            foreach (var record in records)
            {
                var entry = record.GetValue(sourceVariableName);
                if (entry?.HasValue != true)
                    continue;

                var value = entry.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }
        }

        return values.OrderBy(o => o, StringComparer.Ordinal).ToList();
    }
    
    
    private static CodeListOption CreateCodeListOption(CodeListReference codeListReference)
    {
        var display = $"{codeListReference.CodeListRef} {codeListReference.CodeListCode} {codeListReference.CodeListName}";
        return new CodeListOption
        {
            Header =  codeListReference.CodeListRef,
            Content = display,
            CodeListReference = codeListReference
        };
    }

    [RelayCommand]
    private void AddTerm()
    {
        var index = _sourceList.Count+1;
        var termDto = new TermDto(){CodeListUniqueId = CodeListDto.UniqueId,CodeList = _mapper.Map<CodeList>(CodeListDto),Order = index};
        _sourceList.AddOrUpdate(termDto);
        _sourceList.Refresh();
    }
    
    
    [RelayCommand]
    private void InsertTermAbove(TermDto? term)
    {
        if (term == null) return;
        var termOrder = term.Order;
        var dto = new TermDto(){CodeListUniqueId = CodeListDto.UniqueId,CodeList = _mapper.Map<CodeList>(CodeListDto),Order = termOrder};
        _sourceList.Edit((updater) =>
        {
            var terms = updater.Items.Where(o=>o.Order>=termOrder).ToList();
            updater.AddOrUpdate(dto);
            terms.ForEach(o=>o.Order += 1);
            updater.AddOrUpdate(terms);
        });
        
    }
    
    [RelayCommand]
    private void InsertTermBelow(TermDto? term)
    {
        if (term == null) return;
        var termOrder = term.Order;
        var dto = new TermDto(){CodeListUniqueId = CodeListDto.UniqueId,CodeList = _mapper.Map<CodeList>(CodeListDto),Order = termOrder+1};
        _sourceList.Edit((updater) =>
        {
            var terms = updater.Items.Where(o=>o.Order>termOrder).ToList();
            updater.AddOrUpdate(dto);
            terms.ForEach(o=>o.Order += 1);
            updater.AddOrUpdate(terms);
        });
    }
    
    
    [RelayCommand]
    private void Delete(TermDto? term)
    {
        if (term == null) return;
        _sourceList.Edit(changes =>
        {
            changes.RemoveKey(term.Uuid);
            UpdateTermsOrder();
            changes.Refresh();
        });

    }

    private void UpdateTermsOrder()
    {
        int index = 1;
        var terms = _sourceList.Items.OrderBy(o=>o.Order).ToList();
        foreach (var termDto in terms)
        {
            termDto.Order = index;
            index++;
        }
        _sourceList.Edit(changes=>changes.AddOrUpdate(terms));
    }

    private void UpdateTermsDuplicate()
    {

        _sourceList.Edit(list =>
        {
            var dictionary = list.Items.Where(o=>!string.IsNullOrWhiteSpace(o.Name))
                .GroupBy(o=>o.Name).ToDictionary(o=>o.Key,o=>o.ToList());
            foreach (var dictionaryKey in dictionary.Keys)
            {
                bool isDuplicate = dictionary[dictionaryKey].Count > 1;
                foreach (var termDto in dictionary[dictionaryKey])
                {
                    if (termDto.IsNameDuplicate != isDuplicate)
                    {
                        termDto.IsNameDuplicate = isDuplicate;
                    }
                }
            }
        });
    }
    
    private void UpdateDecodedValueConsistent()
    {
        _sourceList.Edit(list =>
        {
            var count = list.Items.Count(o => !string.IsNullOrWhiteSpace(o.DecodedValue));
            var consistent = count == 0 || count == list.Items.Count();
            foreach (var termDto in list.Items)
            {
                if (termDto.DecodedValueConsistent!=consistent)
                {
                    termDto.DecodedValueConsistent = consistent;
                    list.AddOrUpdate(termDto);
                }

            }
        });
    }
    
    [RelayCommand]
    private async Task Save()
    {
        var projectId = _currentProjectService.CurrentProject?.Id ?? 0;
        if (!IsInEditMode && !string.IsNullOrWhiteSpace(CodeListDto.UniqueId))
        {
            var exists = await _sqlSugar.Queryable<CodeList>()
                .Where(o => o.ProjectId == projectId
                            && o.CdiscDataType == _currentProjectService.CdiscDataType
                            && o.UniqueId == CodeListDto.UniqueId)
                .AnyAsync();
            if (exists)
            {
                _messageService.Error($"CodeList {CodeListDto.UniqueId} already exists in the current project.");
                return;
            }
        }

        DetachPropertyChangedHandlers();
        var codeList = _mapper.Map<CodeList>(CodeListDto);
        codeList.ProjectId = projectId;
        codeList.CdiscDataType = _currentProjectService.CdiscDataType;
        var terms = _mapper.Map<List<Term>>(_sourceList.Items);
        foreach (var term in terms)
        {
            term.ProjectId = codeList.ProjectId;
            term.CdiscDataType = codeList.CdiscDataType;
            term.CodeListUniqueId = codeList.UniqueId;
        }
        codeList.Terms = terms;
        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "CodeList", codeList },
                { "Variable", DefaultVariable }
            }
        };
        DialogHost.Close("Root",dialogResult );
    }
    
    private void DetachPropertyChangedHandlers()
    {
        CodeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        foreach (var term in _sourceList.Items)
            DetachTerm(term);
    }

    [RelayCommand]
    private void Cancel()
    {
        DetachPropertyChangedHandlers();
        DialogHost.Close("Root",new DialogResult{Result = ButtonResult.Cancel} );
    }
}


public class TermOptionsAsyncLoader(ISqlSugarClient sqlSugar) : ICompleteOptionsAsyncLoader
{
    public CodeListReference? CodeListReference { get; set; }
    public CodeListStd? CodeListStd { get; set; }

    public async Task<CompleteOptionsLoadResult> LoadAsync(string? context, CancellationToken token)
    {
        List<IAutoCompleteOption> data = [];
        var codeListRef = CodeListReference?.CodeListRef;
        if (!string.IsNullOrWhiteSpace(codeListRef))
        {
            var list = await sqlSugar.AsTenant().QueryableWithAttr<CodeListTerm>()
                .AsWithAttr()
                .Where(o => o.CodeListRef == codeListRef)
                .Where(o => SqlFunc.IsNullOrEmpty(context)
                            || (SqlFunc.IsNullOrEmpty(o.CodeValue) || SqlFunc.Contains(o.CodeValue, context))
                            || (SqlFunc.IsNullOrEmpty(o.DecodedValue) || SqlFunc.Contains(o.DecodedValue, context)))
                .ToListAsync(token);
            foreach (var codeListTerm in list)
            {
                data.Add(new TermCompleteOption
                {
                    Header = $"{codeListTerm.CodeValue} {codeListTerm.DecodedValue}",
                    Content =  codeListTerm.CodeValue,
                    Synonyms = codeListTerm.DecodedValue,
                    SynonymsIsEmpty = string.IsNullOrWhiteSpace(codeListTerm.DecodedValue),
                    CodeListTerm = codeListTerm
                });
            }

            return new CompleteOptionsLoadResult { Data = data };
        }

        if (CodeListStd == null)
            return new CompleteOptionsLoadResult { Data = data };

        var standardTerms = await sqlSugar.Queryable<TermStd>()
            .Where(o => o.CodeListId == CodeListStd.Id)
            .Where(o => SqlFunc.IsNullOrEmpty(context)
                        || (SqlFunc.IsNullOrEmpty(o.Name) || SqlFunc.Contains(o.Name, context))
                        || (SqlFunc.IsNullOrEmpty(o.Synonyms) || SqlFunc.Contains(o.Synonyms, context)))
            .ToListAsync(token);
        foreach (var termStd in standardTerms)
        {
            data.Add(new TermCompleteOption
            {
                Header = termStd.Name,
                Content = $"{termStd.Name} {termStd.Synonyms}",
                Synonyms = termStd.Synonyms,
                SynonymsIsEmpty = string.IsNullOrWhiteSpace(termStd.Synonyms),
                TermStd = termStd
            });
        }

        return new CompleteOptionsLoadResult { Data = data };
    }
}

public record TermCompleteOption : AutoCompleteOption
{
    public string? Synonyms { get; set; }

    public CodeListTerm? CodeListTerm { get; set; }

    public TermStd? TermStd { get; set; }

    public bool SynonymsIsEmpty { get; set; }
}

public class CodeListOption :SelectOption
{
    public CodeListReference? CodeListReference { get; set; }
}

public class VariableOption : SelectOption
{
    public VariableDto Variable { get; set; } = null!;
}