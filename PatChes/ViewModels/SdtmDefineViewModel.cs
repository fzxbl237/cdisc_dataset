using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Xsl;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using AtomUI.Theme.Configuration;
using AtomUI.Theme.Schema;
using Avalonia.Collections;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Enums;
using PatChes.Services;
using PatChes.Services.Interface;
using PatChes.Utils;
using PatChes.Validations;
using PatChes.ViewModels.Defines;
using PatChes.ViewModels.Dialogs;
using PatChes.Views;
using PatChes.Views.Defines;
using PatChes.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using AsyncNavigation.Abstractions;
using PatChes.Models.Settings;
using ReactiveUI;
using SqlSugar;

namespace PatChes.ViewModels;

public partial class SdtmDefineViewModel : ViewModelBase, IDisposable
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly IRegionManager _regionManager;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDefineExcelExportService _defineExcelExportService;
    private readonly IDefineXmlExportService _defineXmlExportService;
    private readonly IMessageService _messageService;
    
    public ThemeConfig SegmentedConfig { get; }

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
        new(){Header = "Documents"},
        new(){Header = "Define Issues"}
    ];
    
    public AvaloniaList<SegmentedItem> SegmentedItems{ get; set; } = [
        new(){Content = "Datasets"},
        new(){Content = "Variables"},
        new(){Content = "ValueLevels"},
        new(){Content = "CodeLists"},  
        new(){Content = "Terms"},
        new(){Content = "Methods"},      
        new(){Content = "Comments"},
        new(){Content = "Dictionaries"},  
        new(){Content = "Documents"},
        new(){Content = "Define Issues"}
    ];

    [ObservableProperty]
    private TabItemData? _selectedTabStripItem;
    
    [ObservableProperty]
    private SegmentedItem? _selectedSegmentedItem;
    

    [ObservableProperty]
    private Dataset? _selectedDataset;
    
    [ObservableProperty]
    private Dataset? _selectedVariable;
    

    public SdtmDefineViewModel(ISqlSugarClient sqlSugar,
        IRegionManager regionManager,
        ICurrentProjectService currentProjectService,
        IDefineExcelExportService defineExcelExportService,
        IDefineXmlExportService defineXmlExportService,
        IMessageService messageService)
    {
        _sqlSugar = sqlSugar;
        _regionManager = regionManager;
        _currentProjectService = currentProjectService;
        _defineExcelExportService = defineExcelExportService;
        _defineXmlExportService = defineXmlExportService;
        _messageService = messageService;
        SegmentedConfig = BuildControlConfig(ControlAlgorithmMode.Global);
        // _sqlSugar.CodeFirst.InitTables<Comment>();
        // _sqlSugar.CodeFirst.InitTables<Variable>();
    }

    private static ThemeConfig BuildControlConfig(ControlAlgorithmMode algorithm)
    {
        return new ThemeConfigBuilder()
            .WithControl(
                new ControlTokenIdentity("AtomUI", "Segmented"),
                new ControlThemeConfigBuilder()
                    .WithAlgorithm(algorithm)
                    .WithToken("ItemActiveBg","#ebedf0")
                    .WithToken("ItemSelectedBg","#000000")
                    .WithToken("ItemSelectedColor","#ffffff")
                    .WithToken("ItemHoverBg","#ebedf0")
                    .Build())
            .Build();
    }
    
    
    

    [RelayCommand]
    private async Task ExportDefineExcelAsync()
    {
        if (_currentProjectService.CurrentProject == null)
        {
            _messageService.Error("Please select a project before exporting");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(AtomUI.Desktop.Controls.Window.GetMainWindow());
        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Define Excel",
            SuggestedFileName = $"define-{DateTime.Now:yyyy-MM-ddTHH-mm-ss-fff}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel File") { Patterns = ["*.xlsx"] }]
        });
        if (file == null)
            return;

        try
        {
            await using var outputStream = await file.OpenWriteAsync();
            await _defineExcelExportService.ExportAsync(outputStream);
            _messageService.Success("Define Excel exported successfully");
        }
        catch (Exception exception)
        {
            _messageService.Error($"Export Define Excel failed: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task PreviewDefineXmlAsync()
    {
        if (_currentProjectService.CurrentProject == null)
        {
            _messageService.Error("Please select a project before previewing");
            return;
        }

        try
        {
            var previewWindow = new DefinePreviewWindow();
            previewWindow.Show();

            var xml = await _defineXmlExportService.GenerateXmlAsync();
            var xsl = LoadPreviewXsl();
            var html = await Task.Run(() => TransformToHtml(xml, xsl));
            previewWindow.SetHtml(html);
        }
        catch (Exception exception)
        {
            _messageService.Error($"Preview Define XML failed: {exception.Message}");
        }
    }

    private static string LoadPreviewXsl()
    {
        using var xslStream = AssetLoader.Open(new Uri("avares://PatChes/Assets/define2-1-0.xsl"));
        using var reader = new StreamReader(xslStream);
        return reader.ReadToEnd();
    }

    private static string TransformToHtml(string xml, string xsl)
    {
        var transformer = new XslCompiledTransform();
        using var xslReader = XmlReader.Create(new StringReader(xsl));
        using var xmlReader = XmlReader.Create(new StringReader(xml));
        using var output = new StringWriter();
        transformer.Load(xslReader);
        transformer.Transform(xmlReader, null, output);
        return output.ToString();
    }

    [RelayCommand]
    private async Task ExportDefineXmlAsync()
    {
        if (_currentProjectService.CurrentProject == null)
        {
            _messageService.Error("Please select a project before exporting");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(AtomUI.Desktop.Controls.Window.GetMainWindow());
        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Define XML",
            SuggestedFileName = $"define-{DateTime.Now:yyyy-MM-ddTHH-mm-ss-fff}.xml",
            DefaultExtension = "xml",
            FileTypeChoices = [new FilePickerFileType("XML File") { Patterns = ["*.xml"] }]
        });
        if (file == null)
            return;

        try
        {
            await using var outputStream = await file.OpenWriteAsync();
            outputStream.SetLength(0);
            await _defineXmlExportService.ExportAsync(outputStream);
            _messageService.Success("Define XML exported successfully");
        }
        catch (Exception exception)
        {
            _messageService.Error($"Export Define XML failed: {exception.Message}");
        }
    }

    partial void OnSelectedSegmentedItemChanged(SegmentedItem? value)
    {
        if (value is { Content: string header })
        {
            if (!string.IsNullOrWhiteSpace(header))
            {
                if (_regionManager.TryGetRegion("SdtmDefineRegion", out _))
                {
                    _ = _regionManager.RequestNavigateAsync("SdtmDefineRegion", header);
                }
            }
        }
    }
    
    [RelayCommand]
    private async Task Load()
    {
        // var list = XmlParser.GetDatasetFromXml(@"C:\Users\zhi\Documents\Pinnacle 21 Community\configs\2508.1\SDTM-IG 3.4 (FDA).xml");
        // var connectionWithAttr = _sqlSugar.AsTenant().GetConnectionWithAttr<DatasetTemplate>();
        // await connectionWithAttr.InsertNav(list).Include(o=>o.Variables).ExecuteCommandAsync();
    }
    
    public void Dispose()
    {
        
    }
    
}