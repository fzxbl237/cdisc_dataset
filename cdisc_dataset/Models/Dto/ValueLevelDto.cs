using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class ValueLevelDto:BaseDto
{
    [ObservableProperty] private int _order;
    
    [ObservableProperty] private string? _dataset;
    
    [ObservableProperty] private int _datasetId;
    
    [ObservableProperty] private Dataset? _datasetEntity; 
    
    [ObservableProperty] private string? _variable;

    partial void OnVariableChanged(string? value)
    {
        VariableExist = !string.IsNullOrWhiteSpace(value);
    }
    
    [ObservableProperty] private int _variableId;
    
    [ObservableProperty] private Variable? _variableEntity; 
    
    [ObservableProperty] private AvaloniaList<WhereClauseDto>? _whereClauses;
    
    //public List<WhereClause>?  WhereClauses { get; set; }
    
    [ObservableProperty] private string? _whereClause;

    partial void OnWhereClauseChanged(string? value)
    {
        WhereClauseExist = !string.IsNullOrWhiteSpace(value);
    }

    [ObservableProperty] private bool _isWhereClauseEffective = true;
    
    [ObservableProperty] private string? _label;
    
    [ObservableProperty] private string? _type;
    
    [ObservableProperty] private int? _length;
    
    [ObservableProperty] private int? _digits;
    
    [ObservableProperty] private string? _format;
    
    [ObservableProperty] private string? _mandatory;
    
    [ObservableProperty] private int _codeListId;
    
    [ObservableProperty] private CodeList? _codeList;
    
    [ObservableProperty] private string? _codeListUniqueId;
    
    [ObservableProperty] private string? _origin;
    
    [ObservableProperty] private string? _source;
    
    [ObservableProperty] private string? _pages;
    
    [ObservableProperty] private int _methodId;
    
    [ObservableProperty] private Method? _method;
    
    [ObservableProperty] private string? _methodUniqueId;
    
    [ObservableProperty] private string? _predecessor;
    
    [ObservableProperty] private int _commentId;
    
    [ObservableProperty] private Comment? _comment;
    
    [ObservableProperty] private string? _commentUniqueId;
    
    [ObservableProperty] private string? _developerNotes;

    [ObservableProperty] private bool _whereClauseExist;

    [ObservableProperty] private bool _variableExist;
    
}