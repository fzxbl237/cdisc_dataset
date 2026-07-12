using System.Collections.Generic;
using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models;

[TenantAttribute("project")]
public class Dataset
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Name { get; set; }
    
    
    [SugarColumn(IsNullable = true)]
    public string? Label { get; set; }
    
    
    [SugarColumn(IsNullable = true)]
    public string? Class { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? SubClass { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Structure { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? KeyVariables { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Standard { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? HasNoData { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Repeating { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? ReferenceData { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int CommentId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CommentId))]
    public Comment? Comment { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? CommentUniqueId{ get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DeveloperNotes { get; set; }
    
    public Language Language { get; set; }
    
    public bool IsTemplate {get;set;}
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    
    public int ProjectId { get; set; }
    
    public CdiscDataType CdiscDataType { get; set; }
    
    [Navigate(NavigateType.OneToMany, nameof(Variable.DatasetId))]
    public List<Variable>?  Variables { get; set; }
    
}