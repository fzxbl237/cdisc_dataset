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
using Prism.Dialogs;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class DictionariesViewModel : ConfirmNavigationViewModelBase
{
    private readonly IDictionaryService _dictionaryService;
    private readonly IMessageService _messageService;
    private readonly IDialogHostService _dialogHostService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<DictionaryDto> _validator;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private CdiscDataType _cdiscDataType;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private AvaloniaList<string> _dataTypeOptions = [.. ConstantOptions.DataTypes];

    [ObservableProperty]
    private AvaloniaList<AutoCompleteOption> _dictionaryNameOptions = [];

    private readonly SourceCache<DictionaryDto, int> _sourceCache = new(o => o.Id);

    private readonly ReadOnlyObservableCollection<DictionaryDto> _dictionarys;
    public ReadOnlyObservableCollection<DictionaryDto> Dictionarys => _dictionarys;

    public DictionariesViewModel(
        IDictionaryService dictionaryService,
        IMessageService messageService,
        IDialogHostService dialogHostService,
        ICurrentProjectService currentProjectService,
        IValidator<DictionaryDto> validator)
    {
        _dictionaryService = dictionaryService;
        _messageService = messageService;
        _dialogHostService = dialogHostService;
        _currentProjectService = currentProjectService;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);

        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _dictionarys, SortExpressionComparer<DictionaryDto>.Ascending(o => o.UniqueId ?? string.Empty))
            .DisposeMany()
            .Subscribe();

        _sourceCache.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(sender =>
            {
                sender?.HasChanged = true;   
                HasChanges = true;
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.UniqueId, false)
            .Subscribe(change =>
            {
                MarkDuplicates();
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(DictionaryDto.UniqueId));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Name, false)
            .Subscribe(change =>
            {
                MarkDuplicates();
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(DictionaryDto.Name));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.HasUniqueIdDuplicate, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, nameof(DictionaryDto.UniqueId));
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.HasNameDuplicate, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, nameof(DictionaryDto.Name));
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Version, false)
            .Subscribe(change =>
            {
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(DictionaryDto.Version));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.DictionaryName, false)
            .Subscribe(change =>
            {
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(DictionaryDto.DictionaryName));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });
    }

    [RelayCommand]
    private async Task AddDictionary()
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Dictionary" }
        };

        var result = await _dialogHostService.ShowDialogAsync("DictionaryDialog", dialogParameters);
        if (!result.Parameters.TryGetValue<DictionaryDto>("Model", out var dictionary) || CurrentProject == null)
            return;

        await _dictionaryService.InsertDictionaryAsync(dictionary);
        _sourceCache.AddOrUpdate(dictionary);
        _messageService.Success("添加成功");
        await LoadDictionaries();
    }

    [RelayCommand]
    private async Task Modify(DictionaryDto dictionary)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Dictionary" },
            { "Model", dictionary }
        };
        var result = await _dialogHostService.ShowDialogAsync("DictionaryDialog", dialogParameters);
        if (!result.Parameters.TryGetValue<DictionaryDto>("Model", out var model) || CurrentProject == null)
            return;

        await _dictionaryService.UpdateDictionaryAsync(model);
        _messageService.Success("Dictionary更新成功");
        await LoadDictionaries();
    }

    [RelayCommand]
    private async Task DeleteAsync(DictionaryDto dictionary)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Dictionary" },
            { "Message", $"Are you sure you want to delete dictionary {dictionary.UniqueId}?" }
        });
        if (result.Result != ButtonResult.OK)
            return;

        await _dictionaryService.DeleteDictionaryAsync(dictionary);
        _sourceCache.Remove(dictionary);
        _messageService.Success("删除成功");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (CurrentProject == null)
            return;

        await _dictionaryService.SaveDictionariesAsync(Dictionarys.ToList());
        HasChanges = false;
        _messageService.Success("Dictionarys Save Success");
        await LoadDictionaries();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || CurrentProject == null)
            return;

        await LoadDictionaries();
        HasChanges = false;
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        navigationContextParameters.TryGetValue("CurrentProject", out Project? currentProject);
        CdiscDataType = cdiscDataType;
        CurrentProject = currentProject;
    }

    public async Task LoadDataAsync()
    {
        if (CurrentProject == null)
            return;

        await LoadDictionaries();
        await LoadDictionaryNameOptions();
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public async Task LoadDictionaries()
    {
        var list = await _dictionaryService.GetAllDictionaryDtosAsync();
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
        MarkDuplicates();
        HasChanges = false;
    }

    public async Task LoadDictionaryNameOptions()
    {
        var names = await _dictionaryService.GetAllDictionaryNamesAsync();
        DictionaryNameOptions.Clear();
        DictionaryNameOptions.AddRange(names
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => new AutoCompleteOption
            {
                Header = o,
                Content = o
            }));
    }

    private void MarkDuplicates()
    {
        _sourceCache.Items.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.HasUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        _sourceCache.Items.MarkDuplicates(
            o => o.Name ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.HasNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    private static Func<DictionaryDto, bool> BuildFilter(string? searchText)
        => SearchFilterExtensions.BuildSearchFilter<DictionaryDto>(
            searchText,
            x => x.UniqueId,
            x => x.Name,
            x => x.DataType,
            x => x.DictionaryName,
            x => x.Version);
}
