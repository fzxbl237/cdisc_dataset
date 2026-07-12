using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class DatasetDto:BaseDto
{
    
    [ObservableProperty] private string? _name;
    
    [ObservableProperty] private string? _label;
    
    [ObservableProperty] private string? _class;  
    
    [ObservableProperty] private string? _subClass;
    
    [ObservableProperty] private string? _structure;
    
    [ObservableProperty] private string? _keyVariables;

    [ObservableProperty] private string? _standard;
    
    [ObservableProperty] private string? _hasNoData;
    
    [ObservableProperty] private string? _repeating;
    
    [ObservableProperty] private string? _referenceData;
    
    [ObservableProperty] private int _commentId;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommentExist))]
    private Comment? _comment;
    
    [ObservableProperty] private string? _commentUniqueId;
    
    public bool CommentExist => Comment != null;
    
    [ObservableProperty] private string? _developerNotes;
    
    [ObservableProperty] private bool _isDuplicate;

    [ObservableProperty] private bool _hasErrors;
    

    public Language Language { get; set; }
    
    public bool IsTemplate {get;set;}
    
    
}