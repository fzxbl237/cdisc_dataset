using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface ICurrentProjectService
{
    Project? CurrentProject { get; set; }
    CdiscDataType CdiscDataType { get; set; }
}