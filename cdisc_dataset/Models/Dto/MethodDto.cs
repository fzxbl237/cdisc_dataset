using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class MethodDto : BaseDto
{
    [ObservableProperty]
    private string? _uniqueId;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _type;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _expressionContext;

    [ObservableProperty]
    private string? _expressionCode;

    [ObservableProperty]
    private string? _pages;

    [ObservableProperty]
    private int _documentId;
    
    [ObservableProperty]
    private Document? _document;
    
    [ObservableProperty]
    private string? _documentUniqueId;

    [ObservableProperty]
    private bool _hasUniqueIdDuplicate;

    [ObservableProperty]
    private bool _hasNameDuplicate;
}