using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class ProjectDto : BaseDto
{
    [ObservableProperty]
    private string? _projectCode;

    [ObservableProperty]
    private string? _protocolCode;

    [ObservableProperty]
    private string? _protocolDescription;

    [ObservableProperty]
    private string? _drugCode;

    [ObservableProperty]
    private SdtmIgVersion _sdtmIgVersion;

    [ObservableProperty]
    private AdamIgVersion _adamIgVersion;

    [ObservableProperty]
    private Language _language;
}
