using PatChes.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PatChes.Models.Dto;

public partial class DocumentDto : BaseDto
{
    [ObservableProperty]
    private string? _uniqueId;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _href;
    

    [ObservableProperty]
    private bool _isUniqueIdDuplicate;

    [ObservableProperty]
    private bool _isTitleDuplicate;

    [ObservableProperty]
    private bool _isHrefDuplicate;
}
