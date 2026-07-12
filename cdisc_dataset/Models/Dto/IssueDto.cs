using System;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Models.Dto;

public class IssueDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public CdiscDataType CdiscDataType { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public string? IssueCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
