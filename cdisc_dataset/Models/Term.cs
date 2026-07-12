using cdisc_dataset.Models.Enums;
using MiniExcelLibs.Attributes;
using SqlSugar;

namespace cdisc_dataset.Models;

[SugarTable("Term")]
[TenantAttribute("project")]
public class Term
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Name { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int CommentId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CommentId))]
    public Comment? Comment { get; set; }
    
    [SugarColumn(IsIgnore=true)]
    public string? CommentUniqueId { get; set; }
    
    public int CodeListId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CodeListId))]
    public CodeList? CodeList { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? CodeListUniqueId { get; set; }
    
    public float Order { get; set; }
    
    
    [SugarColumn(IsNullable = true)]
    public string? Code { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DecodedValue { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    public bool IsNameDuplicate{ get; set; }
    
    public bool DecodedValueConsistent { get; set; } = true;
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId { get; set; }
}


[SugarTable("TermStd")]
[TenantAttribute("project")]
public class TermStd
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    [ExcelIgnore]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("CDISC Synonym(s)")]
    public string? Synonyms { get; set; }
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("CDISC Definition")]
    public string? Definitions { get; set; }
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("NCI Preferred Term")]
    public string? PreferredTerm {get;set;}
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("Codelist Code")]
    public string?  CodelistCode {get;set;}
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("Code")]
    public string?  Code {get;set;}
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("CDISC Submission Value")]
    public string? Name { get; set; }
    
    
    [ExcelIgnore]
    public int CodeListId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CodeListId))]
    [ExcelIgnore]
    public CodeListStd? CodeListStd { get; set; }
    
}