using System;
using cdisc_dataset.Models.Enums;
using FluentValidation.Results;
using SqlSugar;

namespace cdisc_dataset.Models;

public class Variable
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    public int Order { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DatasetName { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? VariableName { get; set; }
    
    
    [SugarColumn(IsNullable = true)]
    public string? Label { get; set; }
    
    
    [SugarColumn(IsNullable = true)]
    public string? DataType { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int? Length { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int? SignificantDigits { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Format { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Mandatory { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? AssignedValue { get; set; }
    
    
    [Navigate(NavigateType.OneToOne, nameof(CodeListId))]
    public CodeList? CodeList { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? CodeListUniqueId { get; set; }
    
    public int CodeListId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Common { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Origin { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Source { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Pages { get; set; }  
    
    public int MethodId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(MethodId))]
    public Method? Method { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? MethodUniqueId { get; set; }
    
    public int DictionaryId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(DictionaryId))]
    public Dictionary? Dictionary { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DictionaryUniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Predecessor { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Role { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? HasNoData { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    [SugarColumn(IsNullable = true)]
    public int CommentId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CommentId))]
    public Comment? Comment { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? CommentUniqueId { get; set; }

    [SugarColumn(IsIgnore=true)]
    public bool IsPremVariable =>
        (Core.Equals("Required") || Core.Equals("Expected") ||
         Core.Equals("Permissible"));

    [SugarColumn(IsNullable = true)]
    public string? Core { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DefaultCodeList { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DeveloperNotes { get; set; }
    
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId { get; set; }
    
    public int DatasetId { get; set; }
    
}