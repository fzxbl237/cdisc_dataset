using SqlSugar;

namespace cdisc_dataset.Models;

[SugarTable("config_dictionary_version")]
[TenantAttribute("project")]
public class DictionaryVersion
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DictionaryName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Version { get; set; }
}
