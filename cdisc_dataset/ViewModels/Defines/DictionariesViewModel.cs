using AsyncNavigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using cdisc_dataset.Constants;
using cdisc_dataset.Controls.DataGrid;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class DictionariesViewModel : ConfirmNavigationViewModelBase, IDataGridDynamicEditorProvider
{
    private readonly IDictionaryService _dictionaryService;
    private readonly IMessageService _messageService;
    private readonly IDialogHostService _dialogHostService;
    private readonly cdisc_dataset.Services.IDialogService _dialogService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<DictionaryDto> _validator;
    private readonly IReferenceDeletionService _referenceDeletionService;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isErrorOnly;

    [ObservableProperty]
    private AvaloniaList<string> _dataTypeOptions = [.. ConstantOptions.DataTypes];

    [ObservableProperty]
    private AvaloniaList<AutoCompleteOption> _dictionaryNameOptions = [];

    private readonly List<DictionaryDto> _allDictionaries = [];
    public AvaloniaList<DictionaryDto> Dictionarys { get; } = [];

    public DictionariesViewModel(
        IDictionaryService dictionaryService,
        IMessageService messageService,
        IDialogHostService dialogHostService,
        cdisc_dataset.Services.IDialogService dialogService,
        ICurrentProjectService currentProjectService,
        IValidator<DictionaryDto> validator,
        IReferenceDeletionService referenceDeletionService)
    {
        _dictionaryService = dictionaryService;
        _messageService = messageService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _currentProjectService = currentProjectService;
        _validator = validator;
        _referenceDeletionService = referenceDeletionService;

    }

    partial void OnIsErrorOnlyChanged(bool value) => RefreshDictionaries();

    private void RefreshDictionaries()
    {
        Dictionarys.Clear();
        Dictionarys.AddRange(_allDictionaries
            .Where(dictionary => !IsErrorOnly || dictionary.HasErrors)
            .OrderBy(dictionary => dictionary.UniqueId, StringComparer.OrdinalIgnoreCase));
    }

    private void DictionaryDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DictionaryDto dictionaryDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(DictionaryDto.HasChanged))
            return;

        if (e.PropertyName == nameof(DictionaryDto.HasErrors))
        {
            RefreshDictionaries();
            return;
        }

        var duplicateFlagProperty = e.PropertyName switch
        {
            nameof(DictionaryDto.IsUniqueIdDuplicate) => nameof(DictionaryDto.UniqueId),
            nameof(DictionaryDto.IsNameDuplicate) => nameof(DictionaryDto.Name),
            nameof(DictionaryDto.IsDictionaryNameDuplicate) => nameof(DictionaryDto.DictionaryName),
            _ => null
        };

        if (duplicateFlagProperty != null)
        {
            Observable.StartAsync(() => _validator.ValidateDtoAsync(dictionaryDto, duplicateFlagProperty));
            return;
        }

        if (e.PropertyName is not (
                nameof(DictionaryDto.UniqueId) or
                nameof(DictionaryDto.Name) or
                nameof(DictionaryDto.DataType) or
                nameof(DictionaryDto.DictionaryName) or
                nameof(DictionaryDto.Version)))
        {
            return;
        }

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
                case nameof(DictionaryDto.DictionaryName):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.DictionaryName));
                    break;
                case nameof(DictionaryDto.Version):
                    await _validator.ValidateDtoAsync(dictionaryDto, nameof(DictionaryDto.Version));
                    break;
            }
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
    private async Task AddDictionaryAsync()
    {
        var result = await _dialogService.ShowAddDictionaryModelAsync();
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<DictionaryDto>("Model", out var dictionary) ||
            _currentProjectService.CurrentProject == null)
            return;

        await _dictionaryService.InsertDictionaryAsync(dictionary);
        RegisterDictionaryDtoPropertyChanged(dictionary);
        _messageService.Success("Dictionary added successfully.");
        await LoadDictionaries();
    }

    [RelayCommand]
    private async Task EditDictionaryAsync(DictionaryDto dictionary)
    {
        var result = await _dialogService.ShowEditDictionaryModelAsync(dictionary);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<DictionaryDto>("Model", out var model) ||
            _currentProjectService.CurrentProject == null)
            return;

        await _dictionaryService.UpdateDictionaryAsync(model);
        _messageService.Success("Dictionary updated successfully.");
        await LoadDictionaries();
    }

    [RelayCommand]
    private async Task DeleteAsync(DictionaryDto dictionary)
    {
        var clearReferences = await _referenceDeletionService.ConfirmReferenceDeletionAsync(
            $"Delete dictionary {dictionary.UniqueId}?",
            "Dictionary",
            await _dictionaryService.ConfirmDictionaryReferenceAsync(dictionary));
        if (clearReferences == null)
            return;

        await _dictionaryService.DeleteDictionaryAsync(dictionary, clearReferences.Value);
        UnregisterDictionaryDtoPropertyChanged(dictionary);
        _allDictionaries.Remove(dictionary);
        MarkDuplicates();
        RefreshDictionaries();
        _messageService.Success("Dictionary deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedDictionaries = _allDictionaries.Where(o => o.IsSelected).ToList();
        if (selectedDictionaries.Count == 0)
        {
            _messageService.Info("Please select at least one dictionary to delete.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Dictionaries" },
            { "Message", $"Are you sure you want to delete {selectedDictionaries.Count} selected dictionary item(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        foreach (var dictionary in selectedDictionaries)
        {
            await _dictionaryService.DeleteDictionaryAsync(dictionary);
            UnregisterDictionaryDtoPropertyChanged(dictionary);
            _allDictionaries.Remove(dictionary);
        }

        MarkDuplicates();
        RefreshDictionaries();
        _messageService.Success($"{selectedDictionaries.Count} dictionary item(s) deleted successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await _dictionaryService.SaveDictionariesAsync(_allDictionaries);
        HasChanges = false;
        _messageService.Success("Dictionaries saved successfully.");
        await LoadDictionaries();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        await LoadDictionaries();
        HasChanges = false;
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
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
        foreach (var dictionaryDto in _allDictionaries)
            UnregisterDictionaryDtoPropertyChanged(dictionaryDto);

        return Task.CompletedTask;
    }

    public async Task LoadDictionaries()
    {
        foreach (var dictionaryDto in _allDictionaries)
            UnregisterDictionaryDtoPropertyChanged(dictionaryDto);

        var list = await _dictionaryService.GetAllDictionaryDtosAsync();
        foreach (var dictionaryDto in list)
        {
            await _validator.ValidateDtoAsync(dictionaryDto);
            RegisterDictionaryDtoPropertyChanged(dictionaryDto);
        }

        _allDictionaries.Clear();
        _allDictionaries.AddRange(list);
        MarkDuplicates();
        RefreshDictionaries();
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

    public async Task<Control?> CreateEditorAsync(
        DataGridDynamicEditorContext context,
        CancellationToken cancellationToken)
    {
        if (context.DataItem is not DictionaryDto dictionaryDto)
            return null;

        if (string.IsNullOrWhiteSpace(dictionaryDto.DictionaryName))
        {
            return new LineEdit
            {
                PlaceholderText = "Please input version"
            };
        }

        var versions = await _dictionaryService
            .GetDictionaryVersionsByDictionaryNameAsync(dictionaryDto.DictionaryName);
        cancellationToken.ThrowIfCancellationRequested();

        if (versions.Count == 0)
        {
            return new LineEdit
            {
                PlaceholderText = "Please input version"
            };
        }

        if (versions.Count <= 12)
        {
            return new AtomUI.Desktop.Controls.ComboBox
            {
                ItemsSource = versions,
                MaxDropDownHeight = 300
            };
        }

        return new AtomUI.Desktop.Controls.ComboBox
        {
            ItemsSource = versions,
            MaxDropDownHeight = 300
        };

        // return new AutoComplete
        // {
        //     MinimumPrefixLength = 0,
        //     IsPopupMatchSelectWidth = true,
        //     OptionsSource = versions.Select(version => new AutoCompleteOption
        //     {
        //         Header = version,
        //         Content = version
        //     })
        // };
    }

    private void MarkDuplicates()
    {
        foreach (var dictionary in _allDictionaries)
        {
            dictionary.IsUniqueIdDuplicate = false;
            dictionary.IsNameDuplicate = false;
            dictionary.IsDictionaryNameDuplicate = false;
        }

        _allDictionaries.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        _allDictionaries.MarkDuplicates(
            o => o.Name ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.IsNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        _allDictionaries.MarkDuplicates(
            o => o.DictionaryName ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.IsDictionaryNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }
}
