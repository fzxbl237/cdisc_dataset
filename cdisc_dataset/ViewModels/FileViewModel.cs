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
using cdisc_dataset.Models.Settings;
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
    private readonly Dictionary<string, Variable?> _standardVariableCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VariableCodeList?> _variableCodeListCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CodeListReference?> _codeListReferenceCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CodeListTerm>> _codeListTermsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, CodeListTerm>> _codeListTermIndexCache = new(StringComparer.OrdinalIgnoreCase);

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
            var replaceResult = await _dialogHostService.ShowDialogAsync(
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

        ClearSdtmImportCaches();
        var (datasets, codeLists) = await BuildSdtmImportAsync(CurrentProject.Id, Files.ToList());
        var (finalCodeLists, codeListDictionary) = await BuildFinalCodeListsAsync(codeLists, CurrentProject.Id);
        LinkCodeListsToVariables(datasets, finalCodeLists, codeListDictionary);
        await _datasetService.InsertDatasetsAsync(datasets);
        _messageService.Success($"Loaded {datasets.Count} dataset(s) from SDTM XPT files");
    }

    private async Task<(List<Dataset> Datasets, List<CodeList> CodeLists)> BuildSdtmImportAsync(
        int projectId,
        List<ProjectFile> files)
    {
        List<Dataset> datasets = [];
        List<CodeList> codeLists = [];

        foreach (var file in files)
        {
            var parsedFile = await Task.Run(() => ParseStandardSdtmFile(file));
            if (parsedFile == null)
                continue;

            var dataset = await BuildDatasetAsync(parsedFile, projectId, codeLists);
            datasets.Add(dataset);
        }

        return (datasets, codeLists);
    }

    private async Task<Dataset> BuildDatasetAsync(
        ParsedSdtmFile parsedFile,
        int projectId,
        List<CodeList> codeLists)
    {
        var name = parsedFile.Name;
        var datasetStd = await _datasetService.GetStandardSdtmDatasetByNameAsync(name);
        var dataset = new Dataset
        {
            Name = name,
            Label = parsedFile.Label,
            Class = datasetStd?.Class,
            Structure = datasetStd?.Structure,
            KeyVariables = datasetStd?.KeyVariables,
            Standard = datasetStd?.Standard,
            HasNoData = parsedFile.HasRecordsAfterRead ? "No" : "Yes",
            Repeating = datasetStd?.Repeating,
            ReferenceData = datasetStd?.ReferenceData,
            ProjectId = projectId,
            CdiscDataType = CdiscDataType.Sdtm
        };

        List<Variable> variables = [];
        foreach (var parsedVariable in parsedFile.Variables)
        {
            var variable = await BuildVariableAsync(name, parsedVariable, projectId);
            var codeList = await BuildCodeListAsync(name, parsedVariable, variable, projectId);
            if (codeList != null)
                codeLists.Add(codeList);

            variables.Add(variable);
        }

        dataset.Variables = variables;
        return dataset;
    }

    private async Task<Variable> BuildVariableAsync(
        string datasetName,
        ParsedSdtmVariable parsedVariable,
        int projectId)
    {
        var variableName = parsedVariable.Name;
        var standardVariable = await GetCachedStandardVariableAsync(datasetName, variableName);
        var origin = variableName.InferOrigin();

        return new Variable
        {
            Order = parsedVariable.Order,
            DatasetName = datasetName,
            VariableName = variableName.ToUpper(),
            Label = parsedVariable.Label,
            DataType = parsedVariable.DataType,
            Length = parsedVariable.DataType == "datetime" ? null : parsedVariable.Length,
            SignificantDigits = parsedVariable.SignificantDigits,
            Format = parsedVariable.Format == "$" ? "$" + parsedVariable.Length : parsedVariable.Format,
            Mandatory = standardVariable?.Mandatory,
            Role = standardVariable?.Role,
            HasNoData = parsedVariable.HasValue ? "No" : "Yes",
            ProjectId = projectId,
            Origin = origin,
            Source = !string.IsNullOrWhiteSpace(origin) ? "Sponsor" : null,
            CdiscDataType = CdiscDataType.Sdtm
        };
    }

    private async Task<CodeList?> BuildCodeListAsync(
        string datasetName,
        ParsedSdtmVariable parsedVariable,
        Variable variable,
        int projectId)
    {
        var variableName = parsedVariable.Name;
        var codeListRef = await GetCachedCodeListRefAsync(variableName.ToUpper());
        if (codeListRef == null)
        {
            return null;
        }

        var codeListRefName = codeListRef.CodeListRef;
        var entries = parsedVariable.Entries
            ?.Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct()
            .ToList();
        var refName = codeListRefName?.Split(".").LastOrDefault();
        var codeListReference = await GetCachedCodeListReferenceAsync(codeListRefName);
        var codeList = new CodeList
        {
            CdiscDataType = CdiscDataType.Sdtm,
            ProjectId = projectId,
            Code = codeListRef.CodeListCode,
            Type = variable.DataType,
            // todo: need dynamic Terminology;
            Terminology = "SDTM 2025-09-26",
            UniqueId = $"{datasetName}.{variableName}.{refName}",
            Name = codeListReference?.CodeListName,
            Terms = await BuildTermsAsync(codeListRefName, entries, projectId)
        };

        // if (codeListRefName == "CL.NY")
        // {
        //     codeList.UniqueId = entries.InferCodeListOid().Split(".").LastOrDefault();
        //     var codeListReference = await _codeListService.GetCodeListReferenceByOidAsync(codeList.UniqueId);
        //     codeList.Name = codeListReference?.CodeListName;
        //     var codeListTerms = await _codeListService.GetCodeListTermsAsync(entries.InferCodeListOid());
        // }else if (codeListRefName == "CL.DOMAIN")
        // {
        //     codeList.UniqueId = $"DOMAIN.{datasetName}";
        //     codeList.Name = $"Domain Abbreviation ({datasetName})";
        // }
        // if (await _codeListService.VariableHasCodeListAsync(variableName))
        // {
        //     var terms = dataEntries.Where(o=>o.HasValue).Select(o=>o.ToString()).Distinct();
        //     
        // }

        return codeList;
    }

    private async Task<List<Term>> BuildTermsAsync(
        string? codeListRefName,
        IEnumerable<string?>? entries,
        int projectId)
    {
        List<Term> terms = [];
        var termOrder = 1;
        foreach (var dataEntry in entries ?? [])
        {
            var codeListTerm = await GetCachedCodeListTermAsync(codeListRefName, dataEntry);
            terms.Add(new Term
            {
                Order = termOrder++,
                Name = dataEntry,
                Code = codeListTerm?.Code,
                DecodedValue = codeListTerm?.DecodedValue,
                CdiscDataType = CdiscDataType.Sdtm,
                ProjectId = projectId
            });
        }

        return terms;
    }

    private async Task<(List<CodeList> FinalCodeLists, Dictionary<string, string?> CodeListDictionary)>
        BuildFinalCodeListsAsync(List<CodeList> codeLists, int projectId)
    {
        Dictionary<string, string?> codeListDictionary = new();
        List<CodeList> finalCodeLists = [];
        var epochCodeLists = codeLists
            .Where(o => o.UniqueId?.EndsWith("EPOCH", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        var codeListsForSplit = codeLists.Except(epochCodeLists).ToList();

        AddEpochCodeList(epochCodeLists, finalCodeLists, codeListDictionary);

        var singleReferenceCodeLists = codeListsForSplit
            .GroupBy(o => o.UniqueId?.Split(".").LastOrDefault())
            .Where(group => group.Count() == 1)
            .SelectMany(group => group)
            .ToList();
        AddSingleReferenceCodeLists(singleReferenceCodeLists, finalCodeLists, codeListDictionary);

        var domainCodeLists = codeListsForSplit
            .GroupBy(o => $"{o.UniqueId?.Split(".").FirstOrDefault()}.{o.UniqueId?.Split(".").LastOrDefault()}")
            .Where(group => group.Count() == 1)
            .SelectMany(group => group)
            .Where(o => !singleReferenceCodeLists.Contains(o))
            .ToList();
        await AddDomainCodeListsAsync(domainCodeLists, projectId, finalCodeLists, codeListDictionary);

        var variableCodeLists = codeListsForSplit
            .Where(o => !singleReferenceCodeLists.Contains(o) && !domainCodeLists.Contains(o))
            .ToList();
        await AddVariableCodeListsAsync(variableCodeLists, projectId, finalCodeLists, codeListDictionary);

        SetTermCodeListUniqueIds(finalCodeLists);
        return (finalCodeLists, codeListDictionary);
    }

    private static void AddEpochCodeList(
        List<CodeList> epochCodeLists,
        List<CodeList> finalCodeLists,
        Dictionary<string, string?> codeListDictionary)
    {
        if (epochCodeLists.Count == 0)
            return;

        var variableWithDatasets = epochCodeLists
            .Select(GetVariableWithDataset)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToList();
        var epochCodeList = epochCodeLists[0];
        epochCodeList.UniqueId = "EPOCH";
        epochCodeList.Terms = epochCodeLists
            .SelectMany(o => o.Terms ?? [])
            .DistinctBy(o => (o.Name, o.Code, o.DecodedValue))
            .Select((term, index) =>
            {
                term.Order = index + 1;
                return term;
            })
            .ToList();
        finalCodeLists.Add(epochCodeList);

        foreach (var variableWithDataset in variableWithDatasets)
            codeListDictionary[variableWithDataset!] = epochCodeList.UniqueId;
    }

    private static void AddSingleReferenceCodeLists(
        List<CodeList> codeLists,
        List<CodeList> finalCodeLists,
        Dictionary<string, string?> codeListDictionary)
    {
        foreach (var codeList in codeLists)
        {
            var variableWithDataset = GetVariableWithDataset(codeList);
            var codeListRef = codeList.UniqueId?.Split(".").LastOrDefault();
            codeList.UniqueId = codeListRef;
            finalCodeLists.Add(codeList);
            if (!string.IsNullOrWhiteSpace(variableWithDataset))
                codeListDictionary.Add(variableWithDataset, codeListRef);
        }
    }

    private async Task AddDomainCodeListsAsync(
        List<CodeList> codeLists,
        int projectId,
        List<CodeList> finalCodeLists,
        Dictionary<string, string?> codeListDictionary)
    {
        foreach (var codeList in codeLists)
        {
            var variableWithDataset = GetVariableWithDataset(codeList);
            var codeListRef = codeList.UniqueId?.Split(".").LastOrDefault();
            var dataset = codeList.UniqueId?.Split(".").FirstOrDefault();

            if (codeListRef == "Y" || codeListRef == "NY" || codeListRef == "ND")
            {
                var inferCodeListOid = codeList.Terms?.Select(o => o.Name).ToList().InferCodeListOid();
                switch (codeListRef)
                {
                    case "Y": inferCodeListOid = "CL.Y"; break;
                    case "ND": inferCodeListOid = "CL.ND"; break;
                }

                codeList.Terms = await GetStandardTermsAsync(inferCodeListOid, projectId, true);
                codeList.UniqueId = codeListRef;
                var codeListReference = await GetCachedCodeListReferenceAsync(inferCodeListOid);
                codeList.Name = codeListReference?.CodeListName;
            }
            else if (!string.IsNullOrWhiteSpace(dataset) && dataset.StartsWith("SUPP") && codeListRef == "DOMAIN")
            {
                var replace = dataset.Replace("SUPP", "");
                codeList.UniqueId = $"{codeListRef}.{replace}";
                codeList.Name = $"{codeList.Name} ({replace})";
            }
            else
            {
                codeList.UniqueId = $"{codeListRef}.{dataset}";
                codeList.Name = $"{codeList.Name} ({dataset})";
            }

            AddCodeListIfHasTerms(codeList, variableWithDataset, finalCodeLists, codeListDictionary);
        }
    }

    private async Task AddVariableCodeListsAsync(
        List<CodeList> codeLists,
        int projectId,
        List<CodeList> finalCodeLists,
        Dictionary<string, string?> codeListDictionary)
    {
        foreach (var codeList in codeLists)
        {
            var variableWithDataset = GetVariableWithDataset(codeList);
            var codeListRef = codeList.UniqueId?.Split(".").LastOrDefault();
            var dataset = codeList.UniqueId?.Split(".").FirstOrDefault();
            var variable = variableWithDataset?.Split(".").LastOrDefault();

            if (codeListRef == "Y" || codeListRef == "NY")
            {
                var inferCodeListOid = codeList.Terms?.Select(o => o.Name).ToList().InferCodeListOid();
                switch (codeListRef)
                {
                    case "Y": inferCodeListOid = "CL.Y"; break;
                    case "NY": inferCodeListOid = "CL.NY"; break;
                }

                codeList.Terms = await GetStandardTermsAsync(inferCodeListOid, projectId, false);
                codeList.UniqueId = codeListRef;
                var codeListReference = await GetCachedCodeListReferenceAsync(codeListRef);
                codeList.Name = codeListReference?.CodeListName;
            }
            else if (codeListRef == "DOMAIN")
            {
                codeList.UniqueId = $"{variable}.{dataset}";
                codeList.Name = variable == "RDOMAIN"
                    ? $"Related Domain Abbreviation ({dataset})"
                    : $"{codeList.Name} ({dataset})";
            }
            else
            {
                codeList.UniqueId = $"{codeListRef}.{variable}";
                codeList.Name = $"{codeList.Name} ({variable})";
            }

            AddCodeListIfHasTerms(codeList, variableWithDataset, finalCodeLists, codeListDictionary);
        }
    }

    private async Task<List<Term>> GetStandardTermsAsync(string? codeListOid, int projectId, bool assignOrder)
    {
        var codeListTerms = await GetCachedCodeListTermsAsync(codeListOid);
        List<Term> terms = [];
        var order = 1;
        foreach (var codeListTerm in codeListTerms)
        {
            terms.Add(new Term
            {
                Name = codeListTerm.CodeValue,
                DecodedValue = codeListTerm.DecodedValue,
                CdiscDataType = CdiscDataType.Sdtm,
                ProjectId = projectId,
                Code = codeListTerm.Code,
                Order = assignOrder ? order++ : 0
            });
        }

        return terms;
    }

    private static void AddCodeListIfHasTerms(
        CodeList codeList,
        string? variableWithDataset,
        List<CodeList> finalCodeLists,
        Dictionary<string, string?> codeListDictionary)
    {
        if (codeList.Terms?.Count <= 0)
            return;

        if (!codeListDictionary.Values.Contains(codeList.UniqueId))
            finalCodeLists.Add(codeList);

        if (!string.IsNullOrWhiteSpace(variableWithDataset))
            codeListDictionary.Add(variableWithDataset, codeList.UniqueId);
    }

    private async Task<Variable?> GetCachedStandardVariableAsync(string datasetName, string variableName)
    {
        var key = $"{datasetName}|{variableName}";
        if (_standardVariableCache.TryGetValue(key, out var cached))
            return cached;

        var standardVariable = await _variableService.GetStandardVariableByDatasetAndVariableNameAsync(
            datasetName,
            variableName,
            CdiscDataType.Sdtm);
        _standardVariableCache[key] = standardVariable;
        return standardVariable;
    }

    private async Task<VariableCodeList?> GetCachedCodeListRefAsync(string variableName)
    {
        if (_variableCodeListCache.TryGetValue(variableName, out var cached))
            return cached;

        var codeListRef = await _codeListService.GetCodeListRefByVariableAsync(variableName);
        _variableCodeListCache[variableName] = codeListRef;
        return codeListRef;
    }

    private async Task<CodeListReference?> GetCachedCodeListReferenceAsync(string? codeListOid)
    {
        if (string.IsNullOrWhiteSpace(codeListOid))
            return null;
        if (_codeListReferenceCache.TryGetValue(codeListOid, out var cached))
            return cached;

        var codeListReference = await _codeListService.GetCodeListReferenceByOidAsync(codeListOid);
        _codeListReferenceCache[codeListOid] = codeListReference;
        return codeListReference;
    }

    private async Task<List<CodeListTerm>> GetCachedCodeListTermsAsync(string? codeListOid)
    {
        if (string.IsNullOrWhiteSpace(codeListOid))
            return [];
        if (_codeListTermsCache.TryGetValue(codeListOid, out var cached))
            return cached;

        var codeListTerms = await _codeListService.GetCodeListTermsAsync(codeListOid);
        _codeListTermsCache[codeListOid] = codeListTerms;
        _codeListTermIndexCache[codeListOid] = codeListTerms
            .Where(o => !string.IsNullOrWhiteSpace(o.CodeValue))
            .GroupBy(o => o.CodeValue!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return codeListTerms;
    }

    private async Task<CodeListTerm?> GetCachedCodeListTermAsync(string? codeListOid, string? codeValue)
    {
        if (string.IsNullOrWhiteSpace(codeListOid) || string.IsNullOrWhiteSpace(codeValue))
            return null;

        await GetCachedCodeListTermsAsync(codeListOid);
        return _codeListTermIndexCache[codeListOid].GetValueOrDefault(codeValue);
    }

    private void ClearSdtmImportCaches()
    {
        _standardVariableCache.Clear();
        _variableCodeListCache.Clear();
        _codeListReferenceCache.Clear();
        _codeListTermsCache.Clear();
        _codeListTermIndexCache.Clear();
    }

    private static string? GetVariableWithDataset(CodeList codeList)
    {
        return codeList.UniqueId?.LastIndexOf('.') switch
        {
            > 0 and var idx => codeList.UniqueId.Substring(0, idx),
            _ => codeList.UniqueId
        };
    }

    private static void SetTermCodeListUniqueIds(List<CodeList> codeLists)
    {
        foreach (var codeList in codeLists)
        {
            if (codeList.Terms == null)
                continue;

            foreach (var term in codeList.Terms)
                term.CodeListUniqueId = codeList.UniqueId;
        }
    }

    private static void LinkCodeListsToVariables(
        List<Dataset> datasets,
        List<CodeList> codeLists,
        Dictionary<string, string?> codeListDictionary)
    {
        var codeListByUniqueId = codeLists.ToDictionary(o => o.UniqueId ?? string.Empty, o => o);
        foreach (var dataset in datasets)
        {
            if (dataset.Variables == null)
                return;

            foreach (var variable in dataset.Variables)
            {
                var oid = $"{variable.DatasetName}.{variable.VariableName}";
                codeListDictionary.TryGetValue(oid, out var codeListRef);
                if (string.IsNullOrWhiteSpace(codeListRef))
                    continue;

                codeListByUniqueId.TryGetValue(codeListRef, out var codeList);
                variable.CodeList = codeList;
                variable.CodeListUniqueId = codeList?.UniqueId;
            }
        }
    }
    private ParsedSdtmFile? ParseStandardSdtmFile(ProjectFile file)
    {
        var storedFile = _liteDatabase.FileStorage.FindById(file.StorageId.ToString());
        if (storedFile == null)
            return null;

        using var memoryStream = new MemoryStream();
        storedFile.CopyTo(memoryStream);
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
        var variableNames = dataSource.GetVariables().ToList();
        var allRecords = new List<DataRecord>();

        while (dataSource.HasRecords())
        {
            try
            {
                var records = dataSource.GetRecords();
                if (records.Count == 0)
                    break;

                allRecords.AddRange(records);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        var variables = new List<ParsedSdtmVariable>(variableNames.Count);
        foreach (var variableName in variableNames)
        {
            var dataEntries = allRecords.Select(o => o.GetValue(variableName)).ToList();
            var dataType = allRecords.InferDataType(variableName);
            var length = Convert.ToInt32(dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Length));
            variables.Add(new ParsedSdtmVariable(
                variableName,
                (string?)dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Label),
                (string?)dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Format),
                Convert.ToInt32(dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Order) ?? 0),
                dataType,
                dataType == "float" ? allRecords.GetDecimalPlaces(variableName) : null,
                length,
                dataEntries.Any(o => o.HasValue),
                dataEntries.Select(o => o.ToString()).ToList()));
        }

        return new ParsedSdtmFile(
            name,
            dataSource.GetDetails().GetString(SourceDetails.Property.DatasetLabel),
            dataSource.HasRecords(),
            variables);
    }

    private sealed record ParsedSdtmFile(
        string Name,
        string? Label,
        bool HasRecordsAfterRead,
        IReadOnlyList<ParsedSdtmVariable> Variables);

    private sealed record ParsedSdtmVariable(
        string Name,
        string? Label,
        string? Format,
        int Order,
        string? DataType,
        int? SignificantDigits,
        int? Length,
        bool HasValue,
        IReadOnlyList<string?>? Entries);

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
