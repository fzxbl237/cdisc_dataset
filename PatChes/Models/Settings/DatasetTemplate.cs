using System.Collections.Generic;
using PatChes.Models.Enums;
using SqlSugar;

namespace PatChes.Models.Settings;


[Tenant("setting")]
[SugarTable("template_dataset")]
public class DatasetTemplate
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
    
    public Language Language { get; set; }
    
    public CdiscDataType CdiscDataType { get; set; }
    
    [Navigate(NavigateType.OneToMany, nameof(VariableTemplate.DatasetId))]
    public List<VariableTemplate>?  Variables { get; set; }
    
}