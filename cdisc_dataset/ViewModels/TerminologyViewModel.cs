using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using cdisc_dataset.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapsterMapper;
using MiniExcelLibs;
using SqlSugar;
using Window = AtomUI.Desktop.Controls.Window;

namespace cdisc_dataset.ViewModels;

public partial class TerminologyViewModel:ObservableObject
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly IMapper _mapper;

    [ObservableProperty]
    private AvaloniaList<ISelectOption> _versionOptions;
    
    [ObservableProperty]
    private IList<ISelectOption> _selectedVersionOptions =[];
    
    [ObservableProperty]    
    private AvaloniaList<CodeListStd> _codeListStds = [];
    

    public TerminologyViewModel(ISqlSugarClient sqlSugar,IMapper mapper)
    {
        _sqlSugar = sqlSugar;
        _mapper = mapper;
        _sqlSugar.CodeFirst.InitTables<CodeList, CodeListStd, Term, TermStd>();
    }

    [RelayCommand] private async Task Upload()
    {
        var topLevel = TopLevel.GetTopLevel(Window.GetMainWindow());
        if(topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Select Terminology File",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Excel File")
            {
                Patterns = ["*.xlsx" ],
                AppleUniformTypeIdentifiers = ["public.xlsx"]  ,
                MimeTypes = ["xlsx/*"] 
            }]
        });
        if (files.Any())
        {
            var file = files.FirstOrDefault();
            if (file != null)
            {
                var path = file.TryGetLocalPath();
                if (path != null)
                {
                    var sheetNames = await MiniExcel.GetSheetNamesAsync(path);
                    var sheet = sheetNames.LastOrDefault();
  
                    if (!string.IsNullOrWhiteSpace(sheet))
                    {
                        var version = sheet.Replace(" Terminology","");
                        await Task.Run(() =>
                        {
                            var list = MiniExcel.Query<CodeListStd>(path, sheet, hasHeader: false)
                                .Where(o => !string.IsNullOrWhiteSpace(o.Extensible)).ToList();
                            var terms = MiniExcel.Query<TermStd>(path, sheet, hasHeader: false)
                                .Where(o => !string.IsNullOrWhiteSpace(o.CodelistCode)).ToList();
                            if (!string.IsNullOrWhiteSpace(version))
                            {
                                list.ForEach(o => o.Terminology = version);
                            }
                            foreach (var codeListStd in list)
                            {
                                var code = codeListStd.Code;
                                var termStds = terms.Where(o=>o.CodelistCode == code).ToList();
                                codeListStd.TermStds = termStds;
                            }

                            var execute = _sqlSugar.InsertNav(list).Include(o=>o.TermStds).ExecuteCommandAsync();

                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if(!execute.Result)
                                    CodeListStds.AddRange(list);
                            });
                            
                        });
                    }
                    
                }
            }
        }
    }
}