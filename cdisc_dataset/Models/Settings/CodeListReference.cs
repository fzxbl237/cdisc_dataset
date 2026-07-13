using SqlSugar;

namespace cdisc_dataset.Models.Settings;

[Tenant("setting")]
[SugarTable("code_list_reference")]
public class CodeListReference
{
    [SugarColumn(IsPrimaryKey = true,IsNullable = true, ColumnName = "code_list_ref")]
    public string? CodeListRef { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_list_code")]
    public string? CodeListCode { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_list_name")]
    public string? CodeListName { get; set; }  
}