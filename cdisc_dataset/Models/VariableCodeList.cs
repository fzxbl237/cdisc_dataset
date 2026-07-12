using MiniExcelLibs.Attributes;
using SqlSugar;

namespace cdisc_dataset.Models;

[TenantAttribute("project")]
public class VariableCodeListProject
{
    [SugarColumn(IsPrimaryKey = true)]
    [ExcelColumnName("Variable Name")]
    public string VariableName { get; set; } = null!;
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("CodeLists")]
    public string? CodeLists { get; set; }
}