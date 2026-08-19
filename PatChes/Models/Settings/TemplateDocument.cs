using PatChes.Models.Enums;
using SqlSugar;

namespace PatChes.Models.Settings;

[Tenant("setting")]
[SugarTable("template_document")]
public class TemplateDocument
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UniqueId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Title { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Href { get; set; }

    public CdiscDataType CdiscDataType { get; set; }
}
