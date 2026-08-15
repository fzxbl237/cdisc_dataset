using System;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;

namespace cdisc_dataset.Services;

public class CurrentProjectService : ICurrentProjectService
{
    private Project? _currentProject;
    private CdiscDataType _cdiscDataType;

    public event Action? Changed;

    public Project? CurrentProject
    {
        get => _currentProject;
        set
        {
            if (ReferenceEquals(_currentProject, value))
                return;

            _currentProject = value;
            Changed?.Invoke();
        }
    }

    public CdiscDataType CdiscDataType
    {
        get => _cdiscDataType;
        set
        {
            if (_cdiscDataType == value)
                return;

            _cdiscDataType = value;
            Changed?.Invoke();
        }
    }
}
