using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Utils;
using cdisc_dataset.Validations;
using cdisc_dataset.ViewModels.Defines;
using cdisc_dataset.ViewModels.Dialogs;
using cdisc_dataset.Views.Defines;
using cdisc_dataset.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using Prism.Dialogs;
using Prism.Ioc;
using Prism.Navigation;
using Prism.Navigation.Regions;
using ReactiveUI;
using SqlSugar;

namespace cdisc_dataset.ViewModels;

public partial class SdtmDefineViewModel:ObservableObject,IDisposable
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly IRegionManager _regionManager;
    private readonly IContainerProvider _container;
    private readonly ICurrentProjectService _currentProjectService;

    private readonly CdiscDataType _cdiscDataType = CdiscDataType.Sdtm;
    public AvaloniaList<TabItemData> TabStripItemDataSource { get; set; } = [
        new(){Header = "Datasets"},
        new(){Header = "Variables"},
        new(){Header = "ValueLevels"},
        new(){Header = "CodeLists"},  
        new(){Header = "Terms"},
        new(){Header = "Methods"},      
        new(){Header = "Comments"},
        new(){Header = "Dictionaries"},  
        new(){Header = "Documents"}
    ];

    [ObservableProperty]
    private TabItemData? _selectedTabStripItem;
    

    [ObservableProperty]
    private Dataset? _selectedDataset;
    
    [ObservableProperty]
    private Dataset? _selectedVariable;
    

    public SdtmDefineViewModel(ISqlSugarClient sqlSugar,
        IRegionManager regionManager,
        IContainerProvider container,
        ICurrentProjectService currentProjectService)
    {
        _sqlSugar = sqlSugar;
        _regionManager = regionManager;
        _container = container;
        _currentProjectService = currentProjectService;
        // _sqlSugar.CodeFirst.InitTables<Comment>();
        // _sqlSugar.CodeFirst.InitTables<Variable>();
    }

    partial void OnSelectedTabStripItemChanged(TabItemData? value)
    {
        if (value is { Header: string header })
        {
            if (!string.IsNullOrWhiteSpace(header))
            {
                var navigationParameters = new NavigationParameters
                {
                    { "CurrentProject", _currentProjectService.CurrentProject },
                    { "CdiscDataType", _cdiscDataType }
                };
                var containsRegionWithName = _regionManager.Regions.ContainsRegionWithName("SdtmDefineRegion");
                if (containsRegionWithName)
                    _regionManager.Regions["SdtmDefineRegion"].RequestNavigate(header, navigationParameters);
            }
        }
    }
    
    [RelayCommand]
    private void Load()
    {
        
        var list = XmlParser.GetDatasetFromXml(@"C:\Users\zhi\Desktop\Temp\SDTM-IG 3.4 (FDA).xml");
        _sqlSugar.CodeFirst.InitTables<Dataset, Variable>();
        _sqlSugar.InsertNav(list).Include(o => o.Variables).ExecuteCommand();
    }
    
    [RelayCommand]
    private void Loaded()
    {
        var navigationParameters = new NavigationParameters
        {
            { "CurrentProject", _currentProjectService.CurrentProject },
            { "CdiscDataType", _cdiscDataType }
        };
        
        var containsRegionWithName = _regionManager.Regions.ContainsRegionWithName("SdtmDefineRegion");
        var commentsView = _container.Resolve<CommentsView>();
        var commentsViewModel = _container.Resolve<CommentsViewModel>();
        commentsView.DataContext = commentsViewModel;
        var mainRegion = _regionManager.Regions["SdtmDefineRegion"];
        mainRegion.Add(commentsView);
        if (containsRegionWithName && SelectedTabStripItem is { Header: string header })
            _regionManager.Regions["SdtmDefineRegion"].RequestNavigate(header,navigationParameters);
    }
    
    public void Dispose()
    {
        
    }
    
}