using SqlSugar;

namespace cdisc_dataset.Models.Settings;

[Tenant("setting")]
[SugarTable("code_list_term")]
public class CodeListTerm
{
    [SugarColumn(IsPrimaryKey = true,IsIdentity = true, ColumnName = "id")]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_list_ref")]
    public string? CodeListRef { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code_value")]
    public string? CodeValue { get; set; }
    
    [SugarColumn(IsNullable = true, ColumnName = "code")]
    public string? Code{ get; set; }  
    
    [SugarColumn(IsNullable = true, ColumnName = "decoded_value")]
    public string? DecodedValue{ get; set; }  
}