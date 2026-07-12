using cdisc_dataset.Models.Enums;
using SqlSugar;

namespace cdisc_dataset.Models;

public class Method
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
    public string? ExpressionContext {get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? ExpressionCode {get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Pages {get; set; }
    
    public int DocumentId {get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(DocumentId))]
    public Document? Document { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DocumentUniqueId { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    [SugarColumn(IsNullable = false)]
    public bool HasUniqueIdDuplicate { get; set; } = false;
    
    [SugarColumn(IsNullable = false)]
    public bool HasNameDuplicate { get; set; } = false;
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId { get; set; }
    
}