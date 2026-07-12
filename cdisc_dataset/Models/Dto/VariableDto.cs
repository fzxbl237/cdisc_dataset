using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class VariableDto:BaseDto
{

    [ObservableProperty] private int _order;
    
    [ObservableProperty] private string? _datasetName;
    
    [ObservableProperty] private string? _variableName;
    
    [ObservableProperty] private string? _label;
    
    [ObservableProperty] private string? _dataType;
    
    [ObservableProperty] private int? _length;
    
    [ObservableProperty] private int? _significantDigits;
    
    [ObservableProperty] private string? _format;
    
    [ObservableProperty] private string? _mandatory;
    
    [ObservableProperty] private string? _assignedValue;

    [ObservableProperty] private CodeList? _codeList;
    
    [ObservableProperty] private string? _codeListUniqueId;
    
    [ObservableProperty] private int _codeListId;
    
    //TODO: 确认Common列的含义及是否需要展示
    [ObservableProperty] private string? _common;
    
    [ObservableProperty] private string? _origin;
    
    [ObservableProperty] private string? _source;
    
    [ObservableProperty] private string? _pages;
    
    [ObservableProperty] private int _methodId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MethodExist))]
    private Method? _method;
    
    [ObservableProperty] 
    private string? _methodUniqueId;
    
    [ObservableProperty] private int _dictionaryId;
    
    [ObservableProperty] private Dictionary? _dictionary;
    
    [ObservableProperty] private string? _dictionaryUniqueId;
    
    
    [ObservableProperty] private string? _predecessor;
    
    [ObservableProperty] private string? _role;
    
    [ObservableProperty] private string? _hasNoData;
    
    [ObservableProperty] private int _commentId;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(CommentExist))]
    private Comment? _comment;
    
    [ObservableProperty] 
    private string? _commentUniqueId;
    
    [ObservableProperty] private bool _isPremVariable;
    
    [ObservableProperty] private string? _core;
    
    [ObservableProperty] private string? _defaultCodeList;
    
    [ObservableProperty] private string? _developerNotes;
    
    
    [ObservableProperty] private int _datasetId;
    
    //[ObservableProperty] private bool _hasChanged;
    
    
    public bool CommentExist =>Comment!=null;
    
    public bool MethodExist =>Method!=null;
    
    
}