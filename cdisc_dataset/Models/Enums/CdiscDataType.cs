using System.ComponentModel;

namespace cdisc_dataset.Models.Enums;

public enum CdiscDataType
{
    [Description("SDTM")]
    Sdtm = 0,
    [Description("ADaM")]
    Adam = 1,
}