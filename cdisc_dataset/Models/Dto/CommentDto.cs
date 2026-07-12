using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using SqlSugar;

namespace cdisc_dataset.Models.Dto;

public partial class CommentDto : BaseDto
{
    
    [ObservableProperty]
    private string? _uniqueId;
    
    [ObservableProperty]
    private string? _description;
    
    [ObservableProperty]
    private int _documentId;
    
    [ObservableProperty]
    private Document? _document;
    
    [ObservableProperty]
    private string? _documentUniqueId;
    
    [ObservableProperty]
    private string? _pages;
    
    
    [ObservableProperty]
    private bool _hasUniqueIdDuplicate;
}
