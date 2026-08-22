using PatChes.Models.Enums;
using SqlSugar;

namespace PatChes.Models.Settings;

[Tenant("setting")]
[SugarTable("template_method")]
public class TemplateMethod
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UniqueId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ExpressionContext { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ExpressionCode { get; set; }

    public CdiscDataType CdiscDataType { get; set; }
}
