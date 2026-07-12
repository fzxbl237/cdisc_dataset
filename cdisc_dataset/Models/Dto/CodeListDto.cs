using System.Collections.Generic;
using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class CodeListDto:BaseDto
{
    
    [ObservableProperty] private string? _uniqueId;
    
    [ObservableProperty] private string? _name;
    
    [ObservableProperty] private string? _code;
    
    [ObservableProperty] private string? _type;
    
    [ObservableProperty] private string? _terminology;
    
    [ObservableProperty] private int _commentId;
    
    [ObservableProperty] private Comment? _comment;
    
    [ObservableProperty] private string? _commentUniqueId;
    
    [ObservableProperty] private string? _developerNotes;
    
    [ObservableProperty] private List<Term>? _terms;
    
    [ObservableProperty] private bool _isDuplicate;
    
    [ObservableProperty] private bool _isNameDuplicate;
    
    [ObservableProperty] private bool _commentExist;

    partial void OnCommentChanged(Comment? value)
    {
        CommentExist = value!=null;
    }



    
}