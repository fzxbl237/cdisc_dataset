using System.Collections.Generic;
using cdisc_dataset.Models.Enums;
using MiniExcelLibs.Attributes;
using SqlSugar;

namespace cdisc_dataset.Models;

[SugarTable("CodeList")]
public class CodeList
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? UniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Name { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Code { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Type { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Terminology { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public int CommentId { get; set; }
    
    [Navigate(NavigateType.OneToOne, nameof(CommentId))]
    public Comment? Comment { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? CommentUniqueId{ get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? DeveloperNotes { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
    
    [Navigate(NavigateType.OneToMany, nameof(Term.CodeListId))]
    public List<Term>?  Terms { get; set; }
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId { get; set; }
}

[SugarTable("CodeListStd")]
[SugarIndex("unique_id",nameof(UniqueId),OrderByType.Asc,nameof(Terminology),OrderByType.Desc,true)]
public class CodeListStd
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    [ExcelIgnore]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("CDISC Submission Value")]
    public string? UniqueId { get; set; }
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("Codelist Extensible (Yes/No)")]
    public string?  Extensible {get;set;}
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("Codelist Name")]
    public string? Name { get; set; }
    
    [SugarColumn(IsNullable = true)]
    [ExcelColumnName("Code")]
    public string? Code { get; set; }
    
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
    [ExcelIgnore]
    public string? Terminology { get; set; }
    
    [Navigate(NavigateType.OneToMany, nameof(TermStd.CodeListId))]
    public List<TermStd>?  TermStds { get; set; }
}