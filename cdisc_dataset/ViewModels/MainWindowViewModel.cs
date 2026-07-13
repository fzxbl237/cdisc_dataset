using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Primitives;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using AtomUI.Desktop.Controls.Primitives;
using AtomUI.Icons;
using AtomUI.Icons.AntDesign;
using Avalonia.Collections;
using Avalonia.Controls;
using cdisc_dataset.Extensions;
using cdisc_dataset.Messages;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRegionManager _regionManager;
    private readonly IProjectService _projectService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private NavMenuNode? _selectedNavMenuItem;

    [ObservableProperty] private Project? _currentProject;
    
    public TreeNodePath DefaultSelectedPath { get; set; } = new("/Projects");

    public AvaloniaList<Project> Projects { get; set; } = [];

    private const string CurrentProjectIdKey = "CurrentProjectId";

    public MainWindowViewModel(
        IRegionManager regionManager,
        IProjectService projectService,
        ICurrentProjectService currentProjectService,
        ISettingsService settingsService)
    {
        _regionManager = regionManager;
        _projectService = projectService;
        _currentProjectService = currentProjectService;
        _settingsService = settingsService;
        WeakReferenceMessenger.Default.Register<ProjectChangedMessage>(this, (_, message) =>
        {
            message.Reply(RefreshProjectsAsync());
        });
        LoadProjects().Await();
    }
    
    public AvaloniaList<NavMenuNode> NavMenuItems { get; set; } =
    [new(){ Header = "Projects",Icon = new ProjectOutlined(),ItemKey = "Projects"},
        new(){ Header = "Files",Icon = new FileAddOutlined(),ItemKey = "Files"},
        new(){ Header = "SDTM Define",Icon = new DatabaseOutlined(),ItemKey = "SdtmDefine"},
        new(){ Header = "Terminology",Icon = new CodeOutlined(),ItemKey = "Terminology"}

    ];

    partial void OnSelectedNavMenuItemChanged(NavMenuNode? value)
    {
        if (value == null)
            return;

        if (value.ItemKey == "SdtmDefine")
        {
            _currentProjectService.CdiscDataType = CdiscDataType.Sdtm;
        }

        _regionManager.Regions["ContentRegion"].RequestNavigate(value.ItemKey.ToString());
    }

    partial void OnCurrentProjectChanged(Project? value)
    {
        _currentProjectService.CurrentProject = value;
        
        if (value?.Id != null)
        {
            _settingsService.SetAsync(CurrentProjectIdKey, value.Id).Await();
            _settingsService.SaveAsync().Await();
        }
    }

    private async Task LoadProjects()
    {
        await RefreshProjectsAsync();
    }

    private async Task<bool> RefreshProjectsAsync()
    {
        Projects.Clear();
        Projects.AddRange(await _projectService.GetAllProjectsAsync());

        await SetCurrentProjectFromSettingsAsync();

        return true;
    }

    private async Task SetCurrentProjectFromSettingsAsync()
    {
        var savedProjectId = await _settingsService.GetAsync<int?>(CurrentProjectIdKey);
        
        if (savedProjectId.HasValue)
        {
            var project = Projects.FirstOrDefault(p => p.Id == savedProjectId.Value);
            if (project != null)
            {
                CurrentProject = project;
                return;
            }
        }
        
        if (Projects.Count > 0)
        {
            CurrentProject = Projects[0];
        }
    }
}