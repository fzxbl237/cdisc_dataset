using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models;

// [SugarIndex("unique_index",nameof(Comment.UniqueId),OrderByType.Asc,nameof(Comment.ProjectId),OrderByType.Asc,true)]
public class Comment
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? UniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }
    
    public int DocumentId {get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(DocumentId))]
    public Document Document { get; set; } = null!;
    
    [SugarColumn(IsNullable = true)]
    public string? DocumentUniqueId {get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Pages {get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId {get; set; }
}