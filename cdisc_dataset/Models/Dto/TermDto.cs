using System;
using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class TermDto:BaseDto
{
    
    [ObservableProperty]
    private string? _name;
    
    [ObservableProperty]
    private int _commentId;
    
    [ObservableProperty]
    private Comment? _comment;

    [ObservableProperty]
    private string? _commentUniqueId;
    
    [ObservableProperty]
    private int _codeListId;
    
    [ObservableProperty]
    private CodeList? _codeList;
    
    [ObservableProperty]
    private string? _codeListUniqueId;
    
    [ObservableProperty]
    private float _order;
    
    [ObservableProperty]
    private string? _code;
    
    [ObservableProperty]
    private string? _decodedValue;
    
    
    [ObservableProperty] 
    private bool _isNameDuplicate;
    
    [ObservableProperty] 
    private bool _decodedValueConsistent = true;

    [ObservableProperty] private string _uuid;

    public TermDto()
    {
        Uuid = Guid.NewGuid().ToString();
    }


}