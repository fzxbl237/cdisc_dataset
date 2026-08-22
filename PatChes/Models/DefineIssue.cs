using System;
using PatChes.Models.Enums;
using SqlSugar;

namespace PatChes.Models;

[Tenant("project")]
public class DefineIssue
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public CdiscDataType CdiscDataType { get; set; }

    [SugarColumn(IsNullable = false, Length = 64)]
    public string Domain { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true, Length = 256)]
    public string? Record { get; set; }

    [SugarColumn(IsNullable = true, Length = 64)]
    public string? Count { get; set; }

    [SugarColumn(IsNullable = true, Length = 512)]
    public string? Variables { get; set; }

    [SugarColumn(IsNullable = true, Length = 512)]
    public string? Values { get; set; }

    [SugarColumn(IsNullable = false, Length = 128)]
    public string Pinnacle21Id { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false, Length = 2048)]
    public string Message { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false, Length = 32)]
    public string Severity { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
