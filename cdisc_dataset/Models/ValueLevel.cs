using System.Collections.Generic;
using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models;

[TenantAttribute("project")]
public class ValueLevel
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    public int Order { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Dataset { get; set; }
    
    public int DatasetId {get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(DatasetId))]
    public Dataset? DatasetEntity { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Variable { get; set; }
    
    public int VariableId {get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(VariableId))]
    public Variable? VariableEntity { get; set; }
    
    [Navigate(NavigateType.OneToMany, nameof(Models.WhereClause.ValueLevelId))]
    public List<WhereClause>?  WhereClauses { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? WhereClause { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Label { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Type { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int? Length { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int? Digits { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Format { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Mandatory { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int CodeListId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CodeListId))]
    public CodeList? CodeList { get; set; }
    
    [SugarColumn(IsIgnore=true)]
    public string? CodeListUniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Origin { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Source { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Pages { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int MethodId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(MethodId))]
    public Method? Method { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? MethodUniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Predecessor { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int CommentId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CommentId))]
    public Comment? Comment { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? CommentUniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DeveloperNotes { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId { get; set; }
    
    
}