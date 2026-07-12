using System;
using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models;

[TenantAttribute("project")]
public class Issue
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public CdiscDataType CdiscDataType { get; set; }

    [SugarColumn(IsNullable = false, Length = 128)]
    public string EntityType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public int EntityId { get; set; }

    [SugarColumn(IsNullable = false, Length = 128)]
    public string PropertyName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false, Length = 1024)]
    public string ErrorMessage { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true, Length = 32)]
    public string? Severity { get; set; }

    [SugarColumn(IsNullable = true, Length = 128)]
    public string? IssueCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
