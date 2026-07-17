using System.Collections.Generic;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;

namespace cdisc_dataset.Constants;

public static class ConstantOptions
{
    public static readonly IReadOnlyList<string> DataTypes =
    [
        "text",
        "integer",
        "float",
        "datetime",
        "date",
        "time",
        "partialDate",
        "partialTime",
        "partialDateTime",
        "incompleteDatetime",
        "durationDatetime",
        "intervalDatetime"
    ];

    public static readonly IReadOnlyList<string> Classes =
    [
        "EVENTS", 
        "FINDINGS", 
        "FINDINGS ABOUT", 
        "INTERVENTIONS",
        "RELATIONSHIP",
        "SPECIAL PURPOSE",
        "STUDY REFERENCE",
        "TRIAL DESIGN"
    ];
    
    public static readonly IReadOnlyList<string> SdtmStandards =
    [
        "",
        "SDTM-IG 3.2", 
        "SDTM-IG 3.3", 
        "SDTM-IG 3.4"
    ];
    
}
