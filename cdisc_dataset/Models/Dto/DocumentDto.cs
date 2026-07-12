using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class DocumentDto : BaseDto
{
    [ObservableProperty]
    private string? _uniqueId;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _href;
    

    [ObservableProperty]
    private bool _hasUniqueIdDuplicate;

    [ObservableProperty]
    private bool _hasTitleDuplicate;
}
