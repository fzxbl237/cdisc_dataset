using PatChes.Models.Enums;
using SqlSugar;

namespace PatChes.Models;

[Tenant("project")]
public class Dictionary
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Name { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DataType { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DictionaryName { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Version { get; set; }
    
    public bool HasErrors { get; set; } = false;
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId {get; set; }
}