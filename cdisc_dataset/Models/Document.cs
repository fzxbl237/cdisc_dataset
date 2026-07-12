using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models;

public class Document
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public string? UniqueId { get; set; }
    
    public string? Title { get; set; }
    
    public string? Href { get; set; }
    
    public CdiscDataType CdiscDataType { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    public int ProjectId { get; set; }
}