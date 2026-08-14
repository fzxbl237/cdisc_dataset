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
using Prism.Dialogs;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class DictionariesViewModel : ConfirmNavigationViewModelBase, IDataGridDynamicEditorProvider
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

    public AvaloniaList<DictionaryDto> Dictionarys { get; } = [];

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

    }

    private void DictionaryDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DictionaryDto dictionaryDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(DictionaryDto.HasChanged))
            return;

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
        Dictionarys.Remove(dictionary);
        MarkDuplicates();
        _messageService.Success("Delete successfully");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (CurrentProject == null)
            return;

        await _dictionaryService.SaveDictionariesAsync(Dictionarys.ToList());
        HasChanges = false;
        _messageService.Success("Dictionaries Save Success");
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
        foreach (var dictionaryDto in Dictionarys)
            UnregisterDictionaryDtoPropertyChanged(dictionaryDto);

        return Task.CompletedTask;
    }

    public async Task LoadDictionaries()
    {
        foreach (var dictionaryDto in Dictionarys)
            UnregisterDictionaryDtoPropertyChanged(dictionaryDto);

        var list = await _dictionaryService.GetAllDictionaryDtosAsync();
        foreach (var dictionaryDto in list)
        {
            await _validator.ValidateDtoAsync(dictionaryDto);
            RegisterDictionaryDtoPropertyChanged(dictionaryDto);
        }

        Dictionarys.Clear();
        Dictionarys.AddRange(list.OrderBy(dictionary => dictionary.UniqueId, StringComparer.OrdinalIgnoreCase));
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
        foreach (var dictionary in Dictionarys)
        {
            dictionary.IsUniqueIdDuplicate = false;
            dictionary.IsNameDuplicate = false;
            dictionary.IsDictionaryNameDuplicate = false;
        }

        Dictionarys.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        Dictionarys.MarkDuplicates(
            o => o.Name ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.IsNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        Dictionarys.MarkDuplicates(
            o => o.DictionaryName ?? string.Empty,
            (dictionary, isDuplicate) => dictionary.IsDictionaryNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }
}
