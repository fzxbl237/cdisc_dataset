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
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

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

    }

    private void DictionaryDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DictionaryDto dictionaryDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(DictionaryDto.HasChanged))
            return;

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(DictionaryDto.UniqueId):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.UniqueId));
                    break;
                case nameof(DictionaryDto.Name):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.Name));
                    break;
                case nameof(DictionaryDto.HasUniqueIdDuplicate):
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.UniqueId));
                    break;
                case nameof(DictionaryDto.HasNameDuplicate):
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.Name));
                    break;
                case nameof(DictionaryDto.Version):
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.Version));
                    break;
                case nameof(DictionaryDto.DictionaryName):
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.DictionaryName));
                    break;
                default:
                    return;
            }

            _sourceCache.AddOrUpdate(dictionaryDto);
        });

        dictionaryDto.HasChanged = true;
        HasChanges = true;
    }

    private void RegisterDictionaryDtoPropertyChanged(DictionaryDto dictionaryDto)
    {
        dictionaryDto.PropertyChanged += DictionaryDtoOnPropertyChanged;
    }

    private void UnregisterDictionaryDtoPropertyChanged(DictionaryDto dictionaryDto)
    {
        dictionaryDto.PropertyChanged -= DictionaryDtoOnPropertyChanged;
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
        RegisterDictionaryDtoPropertyChanged(dictionary);
        _sourceCache.AddOrUpdate(dictionary);
        _messageService.Success("??????");
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
        _messageService.Success("Dictionary???3??");
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
        UnregisterDictionaryDtoPropertyChanged(dictionary);
        _sourceCache.Remove(dictionary);
        _messageService.Success("??????");
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

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        CdiscDataType = _currentProjectService.CdiscDataType;
        CurrentProject = _currentProjectService.CurrentProject;
        return Task.CompletedTask;
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

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var dictionaryDto in _sourceCache.Items)
            UnregisterDictionaryDtoPropertyChanged(dictionaryDto);

        return Task.CompletedTask;
    }

    public async Task LoadDictionaries()
    {
        foreach (var dictionaryDto in _sourceCache.Items)
            UnregisterDictionaryDtoPropertyChanged(dictionaryDto);

        var list = await _dictionaryService.GetAllDictionaryDtosAsync();
        foreach (var dictionaryDto in list)
        {
            await _validator.ValidateDtoAsync(dictionaryDto);
            RegisterDictionaryDtoPropertyChanged(dictionaryDto);
        }

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
