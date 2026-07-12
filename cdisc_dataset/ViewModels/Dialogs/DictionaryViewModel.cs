using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class DictionaryViewModel : ObservableObject, IDialogHostAware
{
    public IReadOnlyList<string> DataTypeOptions { get; } = [
        "text",
        "integer",
        "float",
        "datetime",
        "date",
        "time",
        "partialDate",
        "partialTime",
        "partialDateTime",
        "incompleteDatetime",
        "durationDatetime",
        "intervalDatetime"
    ];

    private readonly WindowMessageManager _messageManager;
    private readonly FormDictionaryValidator _formDictionaryValidator;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDictionaryService _dictionaryService;
    private readonly IValidator<DictionaryDto> _validator;
    
    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _dataDictionaryNameOptions = [];

    public string? DialogHostName { get; set; }

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private DictionaryDto _dictionary = new();

    [ObservableProperty]
    private bool _isInEditMode;

    [ObservableProperty]
    private IList<IFormValidator> _validators = [];

    public DictionaryViewModel(
        WindowMessageManager messageManager,
        FormDictionaryValidator formDictionaryValidator,
        ICurrentProjectService currentProjectService,
        IDictionaryService dictionaryService,
        IValidator<DictionaryDto> validator)
    {
        _messageManager = messageManager;
        _formDictionaryValidator = formDictionaryValidator;
        _currentProjectService = currentProjectService;
        _dictionaryService = dictionaryService;
        _validator = validator;
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");

        Dictionary = parameters.ContainsKey("Model") ? parameters.GetValue<DictionaryDto>("Model") 
            : new DictionaryDto(){ProjectId = _currentProjectService.CurrentProject?.Id??0,
                CdiscDataType = _currentProjectService.CdiscDataType};
        IsInEditMode = Dictionary.Id != 0;
        Dictionary.DataType = string.IsNullOrWhiteSpace(Dictionary.DataType) ? DataTypeOptions[0] : Dictionary.DataType;
        DataDictionaryNameOptions = 
        [
            new AutoCompleteOption{ Header = "MedDRA", Content = "MedDRA" },
            new AutoCompleteOption { Header = "WHODD", Content = "WHODD" },
            new AutoCompleteOption { Header = "LOINC", Content = "LOINC" },
            new AutoCompleteOption { Header = "SNOMED", Content = "SNOMED" },
            new AutoCompleteOption { Header = "UNII", Content = "UNII" },
            new AutoCompleteOption { Header = "NDF-RT/MED-RT", Content = "NDF-RT/MED-RT" }
        ];
        _formDictionaryValidator.Dictionary = Dictionary;
        _formDictionaryValidator.IsInEditMode = IsInEditMode;
        Validators.Add(_formDictionaryValidator);

        Dictionary.PropertyChanged += DictionaryPropertyChanged;
    }

    private void DictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "DictionaryName" && sender is DictionaryDto dictionary)
        {
            LoadDictionaryVersionsAsync(dictionary).Await();
        }
    }
    
    private async Task LoadDictionaryVersionsAsync(DictionaryDto dictionary)
    {
        if (string.IsNullOrWhiteSpace(dictionary.DictionaryName))
        {
            dictionary.Versions = [];
            dictionary.ShowComboBox = false;
            return;
        }

        var versions = await _dictionaryService.GetDictionaryVersionsByDictionaryNameAsync(dictionary.DictionaryName);
        dictionary.Versions = versions;
        dictionary.ShowComboBox = versions.Count > 0;
        if (dictionary.ShowComboBox)
        {
            if (!string.IsNullOrWhiteSpace(dictionary.Version) && !versions.Contains(dictionary.Version))
            {
                dictionary.Version = string.Empty;
            }
        }
        else
        {
            dictionary.Version = string.Empty;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        Dictionary.PropertyChanged -= DictionaryPropertyChanged;
        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "Model", Dictionary } }
        };
        DialogHost.Close(DialogHostName ?? "Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        Dictionary.PropertyChanged -= DictionaryPropertyChanged;
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}
