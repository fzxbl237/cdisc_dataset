using SqlSugar;

namespace cdisc_dataset.Models.Settings;

[Tenant("setting")]
[SugarTable("variable_code_list")]
public class VariableCodeList
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "variable_name")]
    public string? VariableName { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_list_ref")]
    public string? CodeListRef { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_list_code")]
    public string? CodeListCode { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_list_name")]
    public string? CodeListName { get; set; }  
}