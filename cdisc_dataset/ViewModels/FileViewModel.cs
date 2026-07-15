using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dm.util;
using LiteDB;
using P21.Validator.Api.Options;
using P21.Validator.Data;
using Mapster;
using P21.Validator.Api.Models;
using Prism.Dialogs;
using Prism.Navigation.Regions;
using SqlSugar;
using Window = AtomUI.Desktop.Controls.Window;

namespace cdisc_dataset.ViewModels;

public partial class FileViewModel : ObservableObject, INavigationAware
{
    private readonly ILiteDatabase _liteDatabase;
    private readonly IVariableService _variableService;
    private readonly ICodeListService _codeListService;
    private readonly IMessageService _messageService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IDatasetService _datasetService;
    private readonly ILiteCollection<ProjectFile> _files;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private ProjectFileType _selectedFileType = ProjectFileType.Protocol;

    partial void OnSelectedFileTypeChanged(ProjectFileType value)
    {
        LoadFiles();
    }

    public AvaloniaList<ProjectFile> Files { get; } = [];

    public ProjectFileType[] FileTypes { get; } = Enum.GetValues<ProjectFileType>();

    public FileViewModel(
        ILiteDatabase liteDatabase,
        IVariableService variableService,
        ICodeListService  codeListService,
        IMessageService messageService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IDatasetService datasetService)
    {
        _liteDatabase = liteDatabase;
        _variableService = variableService;
        _codeListService = codeListService;
        _messageService = messageService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _datasetService = datasetService;
        _files = _liteDatabase.GetCollection<ProjectFile>("project_files");
        _files.EnsureIndex(x => x.ProjectId);
        _files.EnsureIndex(x => x.FileType);
    }

    [RelayCommand]
    private async Task Upload()
    {
        if (CurrentProject == null || CurrentProject.Id == 0)
        {
            _messageService.Error("Please select a project before uploading files");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(Window.GetMainWindow());
        if (topLevel == null)
            return;

        var storageFiles = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Project Files",
            AllowMultiple = SelectedFileType is ProjectFileType.Sdtm or ProjectFileType.Adam,
            FileTypeFilter = SelectedFileType switch
            {
                ProjectFileType.Protocol or ProjectFileType.Acrf =>
                    [new FilePickerFileType("PDF Files") { Patterns = ["*.pdf"] }],
                ProjectFileType.Sdtm or ProjectFileType.Adam =>
                    [new FilePickerFileType("XPT Files") { Patterns = ["*.xpt"] }],
                _ => []
            }
        });

        if (!storageFiles.Any())
            return;

        var existingFiles = _files.Query()
            .Where(x => x.ProjectId == CurrentProject.Id && x.FileType == SelectedFileType)
            .ToList();

        if (SelectedFileType is ProjectFileType.Protocol or ProjectFileType.Acrf && existingFiles.Any())
        {
            var replaceResult = await _dialogHostService.ShowDialog(
                "ConfirmDialog",
                new DialogParameters
                {
                    { "Title", "Replace existing files" },
                    { "Message", $"{SelectedFileType} already exists in current project. Do you want to replace the existing file(s)?" }
                });

            if (replaceResult.Result != ButtonResult.OK)
                return;

            foreach (var existingFile in existingFiles)
            {
                _liteDatabase.FileStorage.Delete(existingFile.StorageId.ToString());
                _files.Delete(existingFile.Id);
            }
        }

        foreach (var storageFile in storageFiles)
        {
            var localPath = storageFile.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath) || !System.IO.File.Exists(localPath))
                continue;

            await using var stream = System.IO.File.OpenRead(localPath);
            var storageId = ObjectId.NewObjectId();
            _liteDatabase.FileStorage.Upload(storageId.ToString(), storageFile.Name, stream);
            var projectFile = new ProjectFile
            {
                ProjectId = CurrentProject.Id,
                FileType = SelectedFileType,
                FileName = storageFile.Name,
                Size = stream.Length,
                UploadedAt = DateTime.Now,
                StorageId = storageId
            };

            _files.Insert(projectFile);
        }

        LoadFiles();
        _messageService.Success("Files uploaded successfully");
    }

    [RelayCommand]
    private void Delete(ProjectFile? file)
    {
        if (file == null)
            return;

        _liteDatabase.FileStorage.Delete(file.StorageId.ToString());
        _files.Delete(file.Id);
        Files.Remove(file);
    }

    [RelayCommand]
    private async Task LoadStandardSdtmDatasets()
    {
        if (CurrentProject == null || CurrentProject.Id == 0)
        {
            _messageService.Error("Please select a project first");
            return;
        }

        if (SelectedFileType != ProjectFileType.Sdtm)
        {
            _messageService.Error("Please switch to SDTM file type first");
            return;
        }

        var datasets = new List<Dataset>();
        var codeLists = new List<CodeList>();

        foreach (var file in Files)
        {
            var localPath = _liteDatabase.FileStorage.FindById(file.StorageId.ToString());
            if (localPath == null)
                continue;

            await using var memoryStream = new MemoryStream();
            localPath.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var validationOptions = ValidationOptions.CreateBuilder().Build();
            var factory = new DataEntryFactory(validationOptions);
            var name = Path.GetFileNameWithoutExtension(file.FileName).ToUpper();
            var options = SourceOptions.builder()
                .WithName(name)
                .WithMemoryStream(memoryStream)
                .WithType(SourceOptions.StandardTypes.SasTransport)
                .Build();

            using var dataSource = new SasTransportDataSource(options, factory);
            var variableNames = dataSource.GetVariables();
            var allRecords = new List<DataRecord>();

            while (dataSource.HasRecords())
            {
                try
                {
                    var records = dataSource.GetRecords();
                    if (records.Count == 0)
                    {
                        break;
                    }
                    allRecords.AddRange(records);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

            }
            var label = dataSource.GetDetails().GetString(SourceDetails.Property.DatasetLabel);
            var datasetStd = await _datasetService.GetStandardSdtmDatasetByNameAsync(name);
            var dataset = new Dataset()
            {
                Name = name,
                Label = label,
                Class = datasetStd?.Class,
                Structure =  datasetStd?.Structure,
                KeyVariables = datasetStd?.KeyVariables,
                Standard =  datasetStd?.Standard,
                HasNoData =  dataSource.HasRecords()?"No":"Yes",
                Repeating = datasetStd?.Repeating,
                ReferenceData =  datasetStd?.ReferenceData,
                ProjectId = CurrentProject.Id,
                CdiscDataType = CdiscDataType.Sdtm
            };
            
            List<Variable> variables = [];
            foreach (var variableName in variableNames)
            {
                var standardVariable = await _variableService.GetStandardVariableByDatasetAndVariableNameAsync(name, variableName,CdiscDataType.Sdtm);
                var dataEntries = allRecords.Select(o=>o.GetValue(variableName)).ToList();
                var variableLabel = (string?)dataSource.GetVariableProperty(variableName,DataSource.VariableProperty.Label);
                var type = (string?)dataSource.GetVariableProperty(variableName,DataSource.VariableProperty.Type);
                var format = (string?)dataSource.GetVariableProperty(variableName,DataSource.VariableProperty.Format);
                var hasValue = dataEntries.Any(o=>o.HasValue);
                var order = Convert.ToInt32(
                    dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Order) ?? 0);
                var dataType = allRecords.InferDataType(variableName);
                var digit=dataType == "float" ? allRecords.GetDecimalPlaces(variableName) : null;
                int? length = Convert.ToInt32(dataSource.GetVariableProperty(variableName,DataSource.VariableProperty.Length));
                

                var variable = new Variable()
                {
                    Order = order,
                    DatasetName = name,
                    VariableName = variableName.ToUpper(),
                    Label = variableLabel,
                    DataType = dataType,
                    Length = dataType == "datetime" ? null:length,
                    SignificantDigits =  digit,
                    Format = format=="$"?"$"+length:format,
                    Mandatory = standardVariable?.Mandatory,
                    Role =  standardVariable?.Role,
                    HasNoData = hasValue?"No":"Yes",
                    ProjectId = CurrentProject.Id,
                    Origin = variableName.InferOrigin(),
                    Source = !string.IsNullOrWhiteSpace(variableName.InferOrigin())?"Sponsor":null,
                    CdiscDataType = CdiscDataType.Sdtm
                };
                var codeListRef = await _codeListService.GetCodeListRefByVariableAsync(variableName.ToUpper());
                if (codeListRef != null)
                {
                    var codeList = new CodeList();
                    var codeListRefName = codeListRef.CodeListRef;
                    var entries = allRecords
                        .Select(o => o.GetValue(variableName).ToString())
                        .Where(o=>!string.IsNullOrWhiteSpace(o))
                        .Distinct()
                        .ToList();
                    
                    codeList.CdiscDataType = CdiscDataType.Sdtm;
                    codeList.ProjectId = CurrentProject.Id;
                    codeList.Code = codeListRef.CodeListCode;
                    codeList.Type = variable.DataType;
                    // todo: need dynamic Terminology;
                    codeList.Terminology = "SDTM 2025-09-26";
                    var refName = codeListRefName?.Split(".").LastOrDefault();
                    codeList.UniqueId = $"{name}.{variableName}.{refName}";
                    var codeListReference = await _codeListService.GetCodeListReferenceByOidAsync(codeListRefName);
                    codeList.Name = codeListReference?.CodeListName;
                    
                    codeLists.add(codeList);
                    // if (codeListRefName == "CL.NY")
                    // {
                    //     codeList.UniqueId = entries.InferCodeListOid().Split(".").LastOrDefault();
                    //     var codeListReference = await _codeListService.GetCodeListReferenceByOidAsync(codeList.UniqueId);
                    //     codeList.Name = codeListReference?.CodeListName;
                    //     var codeListTerms = await _codeListService.GetCodeListTermsAsync(entries.InferCodeListOid());
                    // }else if (codeListRefName == "CL.DOMAIN")
                    // {
                    //     codeList.UniqueId = $"DOMAIN.{name}";
                    //     codeList.Name = $"Domain Abbreviation ({name})";
                    // }
                    int termOrder = 1;
                    List<Term> terms = [];
                    foreach (var dataEntry in entries)
                    {
                        var term = new Term();
                        term.Order = termOrder++;
                        term.Name = dataEntry;
                        var codeListTerm = await _codeListService.GetCodeListTermAsync(codeListRefName,dataEntry);
                        term.Code = codeListTerm?.Code;
                        term.DecodedValue = codeListTerm?.DecodedValue;
                        term.CdiscDataType = CdiscDataType.Sdtm;
                        term.ProjectId = CurrentProject.Id;
                        terms.Add(term);
                    }

                    codeList.Terms = terms;

                }
                // if (await _codeListService.VariableHasCodeListAsync(variableName))
                // {
                //     var terms = dataEntries.Where(o=>o.HasValue).Select(o=>o.ToString()).Distinct();
                //     
                // }
                variables.Add(variable);
            }

            dataset.Variables = variables;
            datasets.Add(dataset);
        }

        var lists = codeLists.GroupBy(o=>o.UniqueId?.Split(".").LastOrDefault())
            .Where(g=>g.Count()==1)
            .SelectMany(g=>g)
            .ToList();
        Dictionary<string,string?> codeListDictionary = new Dictionary<string, string?>();
        List<CodeList> finalCodeList = [];
        foreach (var codeList in lists)
        {
            var variableWithDataset = codeList.UniqueId?.LastIndexOf('.') switch
            {
                > 0 and var idx => codeList.UniqueId.Substring(0, idx),
                _ => codeList.UniqueId
            };
            var codeListRef = codeList.UniqueId?.Split(".").LastOrDefault();
            codeList.UniqueId = codeListRef;

            finalCodeList.Add(codeList);
            if (!string.IsNullOrWhiteSpace(variableWithDataset))
            {
                codeListDictionary.Add(variableWithDataset, codeListRef);
            }
        }

        var uniqueEachDomain = codeLists.GroupBy(o=>$"{o.UniqueId?.Split(".").FirstOrDefault()}.{o.UniqueId?.Split(".").LastOrDefault()}")
            .Where(g=>g.Count()==1)
            .SelectMany(g=>g)
            .Where(o=>!lists.Contains(o))
            .ToList();
        
        foreach (var codeList in uniqueEachDomain)
        {
            var variableWithDataset = codeList.UniqueId?.LastIndexOf('.') switch
            {
                > 0 and var idx => codeList.UniqueId.Substring(0, idx),
                _ => codeList.UniqueId
            };
            var codeListRef = codeList.UniqueId?.Split(".").LastOrDefault();
            var dataset = codeList.UniqueId?.Split(".").FirstOrDefault();

            if (codeListRef == "Y" || codeListRef == "NY" || codeListRef == "ND")
            {
                var inferCodeListOid = codeList.Terms?.Select(o=>o.Name).ToList().InferCodeListOid();
                switch (codeListRef)
                {
                    case "Y":inferCodeListOid = "CL.Y";break;
                    case "ND":inferCodeListOid = "CL.ND";break;
                }
                var codeListTerms = await _codeListService.GetCodeListTermsAsync(inferCodeListOid);
                List<Term> terms = [];
                int order = 1;
                foreach (var codeListTerm in codeListTerms)
                {
                    var term = new Term
                    {
                        Name = codeListTerm.CodeValue,
                        DecodedValue = codeListTerm.DecodedValue,
                        CdiscDataType = CdiscDataType.Sdtm,
                        ProjectId = CurrentProject.Id,
                        Code = codeListTerm?.Code,
                        Order = order++
                    };
                    terms.Add(term);
                }
                codeList.Terms = terms;
                codeList.UniqueId =  codeListRef;
                var codeListReference= await _codeListService.GetCodeListReferenceByOidAsync(inferCodeListOid);
                codeList.Name = codeListReference?.CodeListName;
            }else if (!string.IsNullOrWhiteSpace(dataset) && dataset.StartsWith("SUPP") && codeListRef=="DOMAIN")
            {
                var replace = dataset.Replace("SUPP","");
                codeList.UniqueId = $"{codeListRef}.{replace}";
                codeList.Name = $"{codeList.Name} ({replace})";
            }else
            {
                codeList.UniqueId = $"{codeListRef}.{dataset}";
                codeList.Name = $"{codeList.Name} ({dataset})";
                
            }

            if (codeList.Terms?.Count > 0)
            {
                if (!codeListDictionary.Values.Contains(codeList.UniqueId))
                {
                    finalCodeList.Add(codeList);
                }
            
                if (!string.IsNullOrWhiteSpace(variableWithDataset))
                {
                    codeListDictionary.Add(variableWithDataset, codeList.UniqueId);
                }
            }

           
        }

        var others = codeLists.Where(o=>!lists.Contains(o) && !uniqueEachDomain.Contains(o))
            .ToList();
        
        foreach (var codeList in others)
        {
            var variableWithDataset = codeList.UniqueId?.LastIndexOf('.') switch
            {
                > 0 and var idx => codeList.UniqueId.Substring(0, idx),
                _ => codeList.UniqueId
            };
            var codeListRef = codeList.UniqueId?.Split(".").LastOrDefault();
            var dataset = codeList.UniqueId?.Split(".").FirstOrDefault();
            var variable = variableWithDataset?.Split(".").LastOrDefault();

            if (codeListRef == "Y" || codeListRef == "NY")
            {
                var inferCodeListOid = codeList.Terms?.Select(o=>o.Name).ToList().InferCodeListOid();
                switch (codeListRef)
                {
                    case "Y":inferCodeListOid = "CL.Y";break;
                    case "NY":inferCodeListOid = "CL.NY";break;
                }
                var codeListTerms = await _codeListService.GetCodeListTermsAsync(inferCodeListOid);
                List<Term> terms = [];
                foreach (var codeListTerm in codeListTerms)
                {
                    var term = new Term
                    {
                        Name = codeListTerm.CodeValue,
                        DecodedValue = codeListTerm.DecodedValue,
                        CdiscDataType = CdiscDataType.Sdtm,
                        ProjectId = CurrentProject.Id,
                        Code = codeListTerm?.Code
                    };
                    terms.Add(term);
                }
                codeList.Terms = terms;
                codeList.UniqueId =  codeListRef;
                var codeListReference= await _codeListService.GetCodeListReferenceByOidAsync(codeListRef);
                codeList.Name = codeListReference?.CodeListName;
            }
            else if (codeListRef == "DOMAIN")
            {
                codeList.UniqueId = $"{variable}.{dataset}";
                codeList.Name = variable == "RDOMAIN" ? $"Related Domain Abbreviation ({dataset})" : $"{codeList.Name} ({dataset})";
            }
            else
            {
                codeList.UniqueId = $"{codeListRef}.{variable}";
                codeList.Name = $"{codeList.Name} ({variable})";
            }


            
            if (codeList.Terms?.Count > 0)
            {
                if (!codeListDictionary.Values.Contains(codeList.UniqueId))
                {
                    finalCodeList.Add(codeList);
                }
            
                if (!string.IsNullOrWhiteSpace(variableWithDataset))
                {
                    codeListDictionary.Add(variableWithDataset, codeList.UniqueId);
                }
            }

           
        }
        
        foreach (var codeList in finalCodeList)
        {
            if (codeList.Terms != null)
            {
                foreach (var codeListTerm in codeList.Terms)
                {
                    codeListTerm.CodeListUniqueId = codeList.UniqueId;
                }
            }
        }

        var dictionary = finalCodeList.ToDictionary(o=>o.UniqueId??string.Empty,o=>o);
        
        foreach (var dataset in datasets)
        {
            if (dataset.Variables == null)
                return;
            foreach (var variable in dataset.Variables)
            {
                var oid = $"{variable.DatasetName}.{variable.VariableName}";
                codeListDictionary.TryGetValue(oid, out string? codeListRef);
                if(string.IsNullOrWhiteSpace(codeListRef))
                    continue;
                dictionary.TryGetValue(codeListRef, out var codeList);
                variable.CodeList = codeList;
                variable.CodeListUniqueId = codeList?.UniqueId;
            }
        }

        await _datasetService.InsertDatasetsAsync(datasets);
        
        _messageService.Success($"Loaded {datasets.Count} dataset(s) from SDTM XPT files");
    }

    [RelayCommand]
    private async Task DeleteProjectData()
    {
       await  _datasetService.DeleteDatasetsByProjectIdAsync(CurrentProject?.Id??0);
       _messageService.Success($"Delete {CurrentProject?.ProjectCode} dataset(s) successfully");
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        CurrentProject = _currentProjectService.CurrentProject;
        LoadFiles();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private void LoadFiles()
    {
        Files.Clear();
        if (CurrentProject == null)
            return;

        var files = _files.Query()
            .Where(x => x.ProjectId == CurrentProject.Id && x.FileType == SelectedFileType)
            .OrderByDescending(x => x.UploadedAt)
            .ToList();

        foreach (var file in files)
        {
            Files.Add(file);
        }
        
    }
}
