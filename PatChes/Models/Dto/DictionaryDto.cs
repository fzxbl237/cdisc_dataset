using System.Collections.Generic;
using AtomUI.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PatChes.Models.Dto;

public partial class DictionaryDto : BaseDto
{
    [ObservableProperty]
    private string? _uniqueId;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _dataType;

    [ObservableProperty]
    private string? _dictionaryName;

    [ObservableProperty]
    private string? _version;

    [ObservableProperty]
    private List<string?>? _versions;
    

    [ObservableProperty]
    private bool _showComboBox;

    [ObservableProperty]
    private bool _isUniqueIdDuplicate;

    [ObservableProperty]
    private bool _isNameDuplicate;

    [ObservableProperty]
    private bool _isDictionaryNameDuplicate;
    
}
