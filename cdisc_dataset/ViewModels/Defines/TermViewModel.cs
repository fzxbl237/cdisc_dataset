using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dm.util;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using Prism.Dialogs;
using Prism.Navigation.Regions;
using SqlSugar;
using DataGridCellPointerPressedEventArgs = Avalonia.Controls.DataGridCellPointerPressedEventArgs;
using DataGridPreparingCellForEditEventArgs = Avalonia.Controls.DataGridPreparingCellForEditEventArgs;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class TermViewModel:ConfirmNavigationViewModelBase
{
    private readonly ITermService _termService;
    private readonly ISqlSugarClient _sqlSugar;
    private readonly ICodeListService _codeListService;
    private readonly ICommentService _commentService;
    private readonly IIssueService _issueService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMessageService _messageService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<TermDto> _validator;
    
    // [ObservableProperty]
    // private AvaloniaList<Comment> _comments = [];
    
    
    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _codeListOptions = [];
    
    private FrozenDictionary<string,CodeList>? _codeListDictionary;
    

    [ObservableProperty] private string? _searchText;
    
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty]
    private CdiscDataType _cdiscDataType;
    
    [ObservableProperty]
    private int _codeListId;
    
    private readonly SourceCache<TermDto,int> _sourceCache = new(o=>o.Id);
    
    private readonly ReadOnlyObservableCollection<TermDto> _terms;
    public ReadOnlyObservableCollection<TermDto> Terms => _terms;
    
    
    [ObservableProperty] private TermOptionsAsyncLoader _termOptionsAsyncLoader;

    public TermViewModel(ITermService termService,
        ISqlSugarClient sqlSugar,
        ICodeListService codeListService,
        ICommentService commentService,
        IIssueService issueService,
        IDialogHostService dialogHostService,
        IMessageService messageService,
        ICurrentProjectService currentProjectService,
        IValidator<TermDto> validator)
    {
        _termService = termService;
        _sqlSugar = sqlSugar;
        _codeListService = codeListService;
        _commentService = commentService;
        _issueService = issueService;
        _dialogHostService = dialogHostService;
        _messageService = messageService;
        _currentProjectService = currentProjectService;
        _validator = validator;
        TermOptionsAsyncLoader = new TermOptionsAsyncLoader(sqlSugar);

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);
        
        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _terms,SortExpressionComparer<TermDto>
                .Ascending(o => o.CodeListUniqueId??string.Empty)
                .ThenByAscending(o=>o.Order))
            .DisposeMany()
            .Subscribe();
        
        _sourceCache.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe((change) =>
            {
                if (!HasChanges)
                    HasChanges = true;
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.CodeListUniqueId, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                var changeValue = change.Value;
                // var autoCompleteOption = CodeListOptions.Where(o=>
                // {
                //     if (o.Content is string stringValue)
                //     {
                //         return stringValue == changeValue;
                //     }
                //     return false;
                // }).FirstOrDefault();
                // if (autoCompleteOption is null)
                // {
                //     changeSender.CodeList = null;
                //     changeSender.CodeListId = 0;
                // }else if (autoCompleteOption is CodeListAutoCompleteOption codeListAutoCompleteOption)
                // {
                //     changeSender.CodeList = codeListAutoCompleteOption.CodeList;
                //     changeSender.CodeListId = codeListAutoCompleteOption.CodeList?.Id ?? 0;
                // }
                
                if (!string.IsNullOrWhiteSpace(changeValue) && _codeListDictionary!=null)
                {
                    _codeListDictionary.TryGetValue(changeValue, out CodeList? codeList);
                    if (codeList != null)
                    {
                        changeSender.CodeList = codeList;
                        changeSender.CodeListId = codeList.Id;
                    }
                    else
                    {
                        changeSender.CodeList = null;
                        changeSender.CodeListId = 0;
                    }
                }
                
                //如果codelist发生改变 term需要重新录入;
                if(!string.IsNullOrWhiteSpace(changeSender.Name))
                    changeSender.Name = string.Empty;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "CodeListUniqueId");
                    _sourceCache.AddOrUpdate(changeSender);
                });
                MarkNameDuplicates();
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.IsNameDuplicate, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "Name");
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Name, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    var termStd = await _termService.GetTermStdAsync(changeSender.CodeList?.Code, change.Value);
                    if (termStd!=null)
                    {
                        changeSender.Code = termStd.Code;
                        if (!string.IsNullOrEmpty(termStd.Synonyms))
                        {
                            changeSender.DecodedValue = termStd.Synonyms.Split(";").First();
                        }
                    }
                    else
                    {
                        changeSender.Code = string.Empty;
                        changeSender.DecodedValue = string.Empty;
                    }
                    MarkNameDuplicates();
                    await _validator.ValidateDtoAsync(changeSender,"Name");
                    _sourceCache.AddOrUpdate(changeSender);
    
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.DecodedValue, false)
            .Subscribe((change) =>
            {
                UpdateDecodedValueConsistent();
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Order, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                _sourceCache.AddOrUpdate(changeSender);
            });
    }
    
    private void UpdateDecodedValueConsistent()
    {
        _sourceCache.Edit(list =>
        {
            var dictionary = list.Items
                .Where(o=>!string.IsNullOrWhiteSpace(o.CodeListUniqueId))
                .GroupBy(o=>o.CodeListUniqueId??string.Empty)
                .ToDictionary(o=>o.Key,o=>o.ToList());
            foreach (var dictionaryKey in dictionary.Keys)
            {
                var count = dictionary[dictionaryKey].Count;
                var countNoEmpty = dictionary[dictionaryKey].Count(o => !string.IsNullOrWhiteSpace(o.DecodedValue));
                bool isConsistent = countNoEmpty==0 || countNoEmpty==count;
                foreach (var termDto in dictionary[dictionaryKey])
                {
                    //todo: need update
                    if (termDto.DecodedValueConsistent != isConsistent)
                    {
                        termDto.DecodedValueConsistent = isConsistent;
                        _validator.ValidateDtoAsync(termDto,"DecodedValue").Await();
                        list.AddOrUpdate(termDto);
                    }
                    // termDto.DecodedValueConsistent = isConsistent;
                    // _validator.ValidateDtoAsync(termDto,"DecodedValue").Await();
                    // list.AddOrUpdate(termDto);
                }
            }
        });
    }
    
    
    

    private static Func<TermDto, bool> BuildFilter(string? searchText)
        => SearchFilterExtensions.BuildSearchFilter<TermDto>(
            searchText,
            x => x.Order.ToString(CultureInfo.InvariantCulture),
            x => x.Name,
            x => x.Code,
            x => x.DecodedValue,
            x => x.CodeListUniqueId);
    
    
    private async Task LoadTerms(int id,CdiscDataType cdiscDataType)
    {
        var list = await _termService.GetAllTermDtosAsync(id,cdiscDataType);
        // list.MarkDuplicates(
        //     o => new {Name=o.Name,
        //         CodeListId=o.CodeListUniqueId},
        //     (term, isDuplicate) => term.IsNameDuplicate = isDuplicate,
        //     key => !string.IsNullOrWhiteSpace(key.Name) && !string.IsNullOrWhiteSpace(key.CodeListId));
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
    }

    private async Task LoadCodeLists(int id, CdiscDataType cdiscDataType)
    {
        var list = await _codeListService.GetAllCodeListsWithoutErorrAsync(id,cdiscDataType);
        List<IAutoCompleteOption> res = [];
        foreach (var codeList in list)
        {
            var autoCompleteOption = new CodeListAutoCompleteOption()
            {
                Header = $"{codeList.UniqueId} {codeList.Name}",
                Content = codeList.UniqueId,
                CodeList = codeList
            };
            res.add(autoCompleteOption);
        }
        _codeListDictionary = list.Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
        CodeListOptions.Clear();
        CodeListOptions.AddRange(res);
    }
    
    // private async Task LoadComments(int id, CdiscDataType cdiscDataType)
    // {
    //     var list = await _commentService.GetAllCommentsAsync(id,cdiscDataType);
    //     List<IAutoCompleteOption> res = [];
    //     foreach (var comment in list)
    //     {
    //         var autoCompleteOption = new AutoCompleteOption(){Header = comment.Description,Content = comment.UniqueId};
    //         res.add(autoCompleteOption);
    //     }
    //     Comments.AddRange(list);
    // }
    
    [RelayCommand]
    private async Task Delete(TermDto termDto)
    {
        await _termService.DeleteTermAsync(termDto);
        _sourceCache.Edit(o =>
        {
            o.Remove(termDto);
        });
        MarkNameDuplicates();
        UpdateDecodedValueConsistent();
        _messageService.Success("Delete Success");
    }
    
    [RelayCommand]
    private async Task AddTerm()
    {
        var termDto = new TermDto()
        {
            Order = 1,
            ProjectId = _currentProjectService.CurrentProject?.Id??0,
            CdiscDataType = CdiscDataType
        };
        await _validator.ValidateDtoAsync(termDto);
        _sourceCache.AddOrUpdate(termDto);
    }
    
    [RelayCommand]
    private async Task Save()
    {
        await _termService.SaveTermsAsync(Terms.ToList());
        _messageService.Success("Terms Save Success");
        await LoadTerms(_currentProjectService.CurrentProject?.Id??0,CdiscDataType);
        HasChanges = false;
    }
    
    [RelayCommand]
    private async Task Discard()
    {
        if(!HasChanges) return;
        await LoadTerms(_currentProjectService.CurrentProject?.Id??0,CdiscDataType);
        HasChanges = false;
    }

    [RelayCommand]
    private async Task PreparingCellForEdit(DataGridPreparingCellForEditEventArgs e)
    {
        if(e.Column.Header is null) return;
        if (e.Column.Header.ToString() != "Term") return;
        TermOptionsAsyncLoader.CodeListStd = null;
        if (e.Row.DataContext is not TermDto termDto) return;
        if (termDto.CodeList == null) return;
        var codeListCode = termDto.CodeList.Code;
        var codeListTerminology = termDto.CodeList.Terminology;
        if (!string.IsNullOrWhiteSpace(codeListCode) && !string.IsNullOrWhiteSpace(codeListTerminology))
        {
            var codeListStd = await _codeListService.GetCodeListStdAsync(codeListTerminology,codeListCode);
            CodeListId = codeListStd.Id;
            TermOptionsAsyncLoader.CodeListStd = codeListStd;
        }
    }

    [RelayCommand]
    private async Task Pair()
    {
        var dialogParameters = new DialogParameters
        {
            { "CdiscDataType", CdiscDataType }
        };
        var result = await _dialogHostService.ShowDialog("PairTermsDialog",dialogParameters);
        if (result.Result == ButtonResult.Yes)
        {
            await LoadTerms(_currentProjectService.CurrentProject?.Id??0,CdiscDataType);
            _messageService.Success("Terms paired success");
        }
    }
    
    private void MarkNameDuplicates()
    {
        _sourceCache.Items.MarkDuplicates(
            o => new {Name=o.Name,
                CodeListId=o.CodeListUniqueId},
            (term, isDuplicate) => term.IsNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key.Name) && !string.IsNullOrWhiteSpace(key.CodeListId));
    }
    
    
    
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType",out CdiscDataType cdiscDataType);
        CdiscDataType = cdiscDataType;
        if (_currentProjectService.CurrentProject != null)
        {
            LoadTerms(_currentProjectService.CurrentProject.Id,CdiscDataType).Await();
            //LoadComments(_currentProjectService.CurrentProject.Id,CdiscDataType).Await();
            LoadCodeLists(_currentProjectService.CurrentProject.Id,CdiscDataType).Await();
        }
    }


    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        if(!HasChanges) return;
        _termService.SaveTermsAsync(Terms.ToList()).Await();
        _messageService.Success("Terms Save Success");
    }
}


public record CodeListAutoCompleteOption : AutoCompleteOption
{
    public CodeList? CodeList { get; set; }
}