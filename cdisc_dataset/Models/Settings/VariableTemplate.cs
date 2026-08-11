using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models.Settings;


[Tenant("setting")]
[SugarTable("template_variable")]
public class VariableTemplate
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
    public string? Mandatory { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Role { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Core { get; set; }
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int DatasetId { get; set; }
    
}