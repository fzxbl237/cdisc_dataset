using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Dm.util;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using MiniExcelLibs;
using Prism.Dialogs;
using ReactiveUI;
using ReactiveUI.Builder;
using SqlSugar;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class AddCodeListViewModel: ObservableObject, IDialogHostAware
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly ICommentService _commentService;
    private readonly IValidator<TermDto> _validator;
    private readonly IMapper _mapper;
    public string? DialogHostName { get; set; } = "Root";
    
    //public AvaloniaList<Comment> Comments { get; set; } = [];
    
    public AvaloniaList<string> Types { get; set; } = ["text", "integer", "float","datetime","date","time",
        "partialDate","partialTime","partialDateTime","incompleteDatetime","durationDatetime","intervalDatetime"];

    public AvaloniaList<string?> Terminologies  { get; set; } = [];
    
    [ObservableProperty]
    private AvaloniaList<ISelectOption> _codeLists = [];
    
    [ObservableProperty] private ISelectOption? _selectedCodeList;
    
    [ObservableProperty]
    private AvaloniaList<ISelectOption> _comments = [];
    
    [ObservableProperty]
    private ISelectOption? _selectedComment;
    
    [ObservableProperty]
    private AvaloniaList<ISelectOption>? _variables;

    [ObservableProperty] private ISelectOption? _selectedVariable;
    
    [ObservableProperty] private int _currentProjectId;
    
    [ObservableProperty] private CdiscDataType _cdiscDataType;
    
    [ObservableProperty] private CodeListDto _codeListDto = new();
    
    private readonly SourceCache<TermDto,string> _sourceList= new (o=>o.Uuid);
    
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private int _codeListStdId;
    
    private readonly ReadOnlyObservableCollection<TermDto> _terms;
    public ReadOnlyObservableCollection<TermDto> Terms => _terms;
    

    [ObservableProperty] private TermOptionsAsyncLoader _termOptionsAsyncLoader;
    
    private readonly CompositeDisposable _disposables = new();

    public AddCodeListViewModel(ISqlSugarClient sqlSugar,ICurrentProjectService currentProjectService,ICommentService commentService,IValidator<TermDto> validator,IMapper mapper)
    {
        _sqlSugar = sqlSugar;
        _currentProjectService = currentProjectService;
        _commentService = commentService;
        _validator = validator;
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
        
        observableCache.Connect()
            .WhenPropertyChanged(o=>o.Name,notifyOnInitialValue:false)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .DistinctUntilChanged()
            .Do((change) =>
                {

                    var termStd = _sqlSugar.Queryable<TermStd>()
                        .Where(o => o.CodeListId == CodeListStdId)
                        .Where(o => o.Name == change.Value)
                        .First();
                    if (termStd != null)
                    {
                        change.Sender.Code = termStd.Code;
                        if (!string.IsNullOrWhiteSpace(termStd.Synonyms))
                        {
                            change.Sender.DecodedValue = termStd.Synonyms.Split(";").FirstOrDefault();
                        }
                    }
                    else
                    {
                        change.Sender.Code = string.Empty;
                        change.Sender.DecodedValue = string.Empty;
                    }

                    UpdateTermsDuplicate();
                })
            .Subscribe((change) =>
            {
                ValidateTermDtoAsync(change.Sender).Await();
                _sourceList.AddOrUpdate(change.Sender);
            }).DisposeWith(_disposables);
        
        observableCache.Connect()
            .DisposeMany()
            .WhereReasonsAre(ChangeReason.Remove)
            .Subscribe(changeSet =>
            {
                UpdateTermsDuplicate();
            });
        
        observableCache.Connect()
            .DisposeMany()
            .WhenPropertyChanged(x=>x.IsNameDuplicate,false)
            .SubscribeOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .DistinctUntilChanged()
            .Subscribe( (change) =>
            {
                ValidateTermDtoAsync(change.Sender).Await();
                _sourceList.AddOrUpdate(change.Sender);
            });
        
        observableCache.Connect()
            .DisposeMany()
            .WhenPropertyChanged(x=>x.Order,false)
            .SubscribeOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .DistinctUntilChanged()
            .Subscribe( (change) =>
            {
                UpdateTermsOrder();
            });
        
        observableCache.Connect()
            .DisposeMany()
            .WhenPropertyChanged(x=>x.DecodedValue,false)
            .SubscribeOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .DistinctUntilChanged()
            .Subscribe( (change) =>
            {
                UpdateDecodedValueConsistent();
            });
        
    }



    private static Func<TermDto, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return trade => true;
        return o => Contains(searchText, o.Order.ToString(CultureInfo.InvariantCulture))
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
        parameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        CdiscDataType = cdiscDataType;
        if (_currentProjectService.CurrentProject != null) 
            CurrentProjectId = _currentProjectService.CurrentProject.Id;
        Terminologies.Clear();
        var list = _sqlSugar.Queryable<CodeListStd>().Select(o=>o.Terminology).Distinct().ToList();
        Terminologies.Add(" ");
        Terminologies.AddRange(list);
        LoadComments().Await();
        LoadVariables().Await();
        if (Terminologies.Count >= 2)
        {
            var terminology = Terminologies[1];
            CodeListDto.Terminology = terminology;
            LoadCodeLists(terminology,[]).Await();
        }

        this.WhenPropertyChanged(x => x.CodeListDto.Terminology)
            .Select(o => o.Value)
            .Subscribe(s =>
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    CodeLists.Clear();
                    SelectedCodeList = null;
                }
                else
                {
                    LoadCodeLists(s,GetCodeLists(SelectedVariable)).Await();
                }
            });
        CodeListDto.Type = Types[0];
    }
    

    partial void OnSelectedVariableChanged(ISelectOption? value)
    {
        var codeLists = GetCodeLists(value);
        LoadCodeLists(CodeListDto.Terminology, codeLists).Await();
        SelectedCodeList = CodeLists?.FirstOrDefault();
    }
    
    partial void OnSelectedCodeListChanged(ISelectOption? value)
    {
        if (value == null)
        {
            CodeListDto.UniqueId =string.Empty;
            CodeListDto.Name =  string.Empty;
            CodeListDto.Code =  string.Empty;
            return;
        }
        if (value.Content is not CodeListStd codeListStd) return;
        CodeListDto.Name =  codeListStd.Name;
        CodeListDto.Code =  codeListStd.Code;
        CodeListDto.UniqueId = codeListStd.UniqueId;
        TermOptionsAsyncLoader.CodeListStd = codeListStd;
        CodeListStdId = codeListStd.Id;
        _sourceList.Edit((changes) =>
        {
            foreach (var changesItem in changes.Items)
            {
                changesItem.CodeListUniqueId = CodeListDto.UniqueId;
                changesItem.CodeList = _mapper.Map<CodeList>(CodeListDto);
                ValidateTermDtoAsync(changesItem).Await();
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
    
    private List<string> GetCodeLists(ISelectOption? value)
    {
        if (value == null || value.Content is not VariableCodeList variableCodeList) return [];
        var codeLists = variableCodeList.CodeLists;
        return string.IsNullOrWhiteSpace(codeLists) ? [] : codeLists.Split("; ").ToList();
    }
    
    private async Task LoadComments()
    {
        if(_currentProjectService.CurrentProject==null) return;
        var comments =  await _commentService.GetAllCommentsAsync(_currentProjectService.CurrentProject.Id,CdiscDataType);
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

    private async Task LoadVariables()
    {
        var list =  await _sqlSugar.Queryable<VariableCodeList>().ToListAsync();
        List<ISelectOption> res = [];
        foreach (var variableCodeList in list)
        {
            var selectOption = new SelectOption() { Header = variableCodeList.VariableName,Content = variableCodeList };
            res.Add(selectOption);
        }
        Variables = new AvaloniaList<ISelectOption>(res);
    }
    
    private async Task LoadCodeLists(string? terminology,List<string> codeLists)
    {
        
        var codeListStds = await _sqlSugar.Queryable<CodeListStd>()
            .Where(o=>o.Terminology == terminology 
                      && (codeLists.Count==0 || string.IsNullOrWhiteSpace(o.Code) || codeLists.Contains(o.Code))).ToListAsync();
        List<ISelectOption> res = [];
        foreach (var codeListStd in codeListStds)
        {
            if(string.IsNullOrWhiteSpace(codeListStd.UniqueId) || string.IsNullOrWhiteSpace(codeListStd.Name))
                continue;
            var selectOption = new SelectOption() { Header = codeListStd.UniqueId,Content = codeListStd };
            res.Add(selectOption);
        }
        CodeLists.Clear();
        //CodeLists = new AvaloniaList<ISelectOption>(res);
        CodeLists.AddRange(res);
    }
    
    private async Task ValidateTermDtoAsync(TermDto termDto)
    {
        termDto.ClearErrors();
        var result = await _validator.ValidateAsync(termDto);
        foreach (var validationFailure in result.Errors)
        {
            termDto.SetError(validationFailure.PropertyName,
                new DataGridValidationResult(validationFailure.ErrorMessage,
                    validationFailure.Severity==Severity.Error?DataGridValidationSeverity.Error
                        :DataGridValidationSeverity.Warning));
        }
    }
    
    [RelayCommand]
    private async Task AddTerm()
    {
        var index = _sourceList.Count+1;
        var termDto = new TermDto(){CodeListUniqueId = CodeListDto.UniqueId,CodeList = _mapper.Map<CodeList>(CodeListDto),Order = index};
        await ValidateTermDtoAsync(termDto);
        _sourceList.AddOrUpdate(termDto);
        _sourceList.Refresh();
    }
    
    
    [RelayCommand]
    private async Task InsertTermAbove(TermDto? term)
    {
        if (term == null) return;
        var termOrder = term.Order;
        var dto = new TermDto(){CodeListUniqueId = CodeListDto.UniqueId,CodeList = _mapper.Map<CodeList>(CodeListDto),Order = termOrder};
        await ValidateTermDtoAsync(dto);
        _sourceList.Edit((updater) =>
        {
            var terms = updater.Items.Where(o=>o.Order>=termOrder).ToList();
            updater.AddOrUpdate(dto);
            terms.ForEach(o=>o.Order += 1);
            updater.AddOrUpdate(terms);
        });
        
    }
    
    [RelayCommand]
    private async Task InsertTermBelow(TermDto? term)
    {
        if (term == null) return;
        var termOrder = term.Order;
        var dto = new TermDto(){CodeListUniqueId = CodeListDto.UniqueId,CodeList = _mapper.Map<CodeList>(CodeListDto),Order = termOrder+1};
        await ValidateTermDtoAsync(dto);
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
                    ValidateTermDtoAsync(termDto).Await();
                    list.AddOrUpdate(termDto);
                }

            }
        });
    }
    
    [RelayCommand]
    private void Save()
    {
        var codeList = _mapper.Map<CodeList>(CodeListDto);
        codeList.ProjectId = CurrentProjectId;
        codeList.CdiscDataType = CdiscDataType;
        var terms = _mapper.Map<List<Term>>(_sourceList.Items);
        foreach (var term in terms)
        {
            term.ProjectId = CurrentProjectId;
            term.CdiscDataType = CdiscDataType;
        }
        codeList.Terms = terms;
        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters{{"CodeList",codeList}}
        };
        DialogHost.Close("Root",dialogResult );
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root",new DialogResult{Result = ButtonResult.Cancel} );
    }
}

public class TermOptionsAsyncLoader(ISqlSugarClient sqlSugar) : ICompleteOptionsAsyncLoader
{
    private readonly ISqlSugarClient _sqlSugar = sqlSugar;
    public CodeListStd? CodeListStd { get; set; }
    

    public async Task<CompleteOptionsLoadResult> LoadAsync(string? context, CancellationToken token)
    {

        List<IAutoCompleteOption> data   = [];
        if (CodeListStd == null)
            return new CompleteOptionsLoadResult()
            {
                Data = data
            };
        var list = await _sqlSugar.Queryable<TermStd>()
            .Where(o=>o.CodeListId == CodeListStd.Id)
            .Where(o=> (SqlFunc.IsNullOrEmpty(context)
                        || (SqlFunc.IsNullOrEmpty(o.Name) || SqlFunc.Contains(o.Name,context))
                        || (SqlFunc.IsNullOrEmpty(o.Synonyms) || SqlFunc.Contains(o.Synonyms,context))
                )).ToListAsync(token);
        foreach (var termStd in list)
        {
            data.Add(new TermCompleteOption()
            {
                Header = $"{termStd.Name} {termStd.Synonyms}",
                Content =  $"{termStd.Name} {termStd.Synonyms}",
                Synonyms = termStd.Synonyms,
                SynonymsIsEmpty = string.IsNullOrWhiteSpace(termStd.Synonyms),
                TermStd = termStd
            });
        }

        return new CompleteOptionsLoadResult()
        {
            Data = data
        };
        
    }
}

public record TermCompleteOption : AutoCompleteOption
{
    public string? Synonyms { get; set; }
    
    public TermStd? TermStd { get; set; }
    
    public bool SynonymsIsEmpty { get; set; }
}