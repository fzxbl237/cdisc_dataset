using System;
using PatChes.Models;
using PatChes.Models.Enums;

namespace PatChes.Services.Interface;

public interface ICurrentProjectService
{
    Project? CurrentProject { get; set; }
    CdiscDataType CdiscDataType { get; set; }
    event Action? Changed;
}
