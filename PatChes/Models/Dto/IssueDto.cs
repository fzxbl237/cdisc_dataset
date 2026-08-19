using System;
using PatChes.Models.Enums;

namespace PatChes.Models.Dto;

public class IssueDto
{
    public int Id { get; set; }
    public bool IsSelected { get; set; }
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
