using SqlSugar;

namespace cdisc_dataset.Models;

public class WhereClause
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    public int ValueLevelId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Variable { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(VariableId))]
    public Variable? VariableEntity { get; set; }
    
    public int VariableId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Comparator { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Values { get; set; }
}