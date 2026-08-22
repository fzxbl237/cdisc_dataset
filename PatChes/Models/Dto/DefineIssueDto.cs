using System;
using PatChes.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PatChes.Models.Dto;

public partial class DefineIssueDto : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isSelected;

    public int ProjectId { get; set; }
    public CdiscDataType CdiscDataType { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string? Record { get; set; }
    public string? Count { get; set; }
    public string? Variables { get; set; }
    public string? Values { get; set; }
    public string Pinnacle21Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
