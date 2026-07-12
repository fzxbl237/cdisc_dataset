using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;

namespace cdisc_dataset.Services;

public class CurrentProjectService : ICurrentProjectService
{
    public Project? CurrentProject { get; set; }

    public CdiscDataType CdiscDataType { get; set; }
}