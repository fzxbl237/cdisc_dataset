using System.Collections.Generic;
using System.ComponentModel;
using SqlSugar;

namespace cdisc_dataset.Models;

public class Project
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    public string? ProjectCode { get; set; }
    
    [SugarColumn(IsNullable = true)]
    public string? ProtocolCode { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? ProtocolDescription { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? DrugCode { get; set; }
    
    public SdtmIgVersion SdtmIgVersion { get; set; }
    
    public AdamIgVersion AdamIgVersion { get; set; }
    
    public Language Language{get;set;}
    
    [Navigate(NavigateType.OneToMany, nameof(Dataset.ProjectId))]
    public List<Dataset>  Datasets { get; set; }
    
    [SugarColumn(IsNullable = false)]
    public bool HasErrors { get; set; } = false;
}

public enum SdtmIgVersion
{
    [Description("3.2")]
    SDTMIG3_2 = 0,
    [Description("3.3")]
    SDTMIG3_3 = 1,
    [Description("3.4")]
    SDTMIG3_4 = 2
}

public enum AdamIgVersion
{
    [Description("1.1")]
    ADAMIG1_1 = 0,
    [Description("1.2")]
    ADAMIG1_2 = 1,
    [Description("1.3")]
    ADAMIG1_3 = 2
     
}

public enum Language
{
    [Description("English")]
    En = 0,
    [Description("Chinese")]
    Zh = 1
}