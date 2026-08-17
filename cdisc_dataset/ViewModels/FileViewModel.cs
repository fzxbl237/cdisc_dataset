using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using AsyncNavigation;
using SqlSugar;
using Window = AtomUI.Desktop.Controls.Window;

namespace cdisc_dataset.ViewModels;

public partial class FileViewModel : ViewModelBase
{
    private readonly ILiteDatabase _liteDatabase;
    private readonly ISqlSugarClient _sqlSugar;
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
        ISqlSugarClient sqlSugar,
        IVariableService variableService,
        ICodeListService  codeListService,
        IMessageService messageService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IDatasetService datasetService)
    {
        _liteDatabase = liteDatabase;
        _sqlSugar = sqlSugar;
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
            await using var stream = await storageFile.OpenReadAsync();
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
        _messageService.Success($"{storageFiles.Count} file(s) uploaded successfully");
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
        var (datasets, codeLists, parsedFiles) = await BuildSdtmImportAsync(CurrentProject.Id, Files.ToList());
        var (finalCodeLists, codeListDictionary) = await BuildFinalCodeListsAsync(codeLists, CurrentProject.Id);
        LinkCodeListsToVariables(datasets, finalCodeLists, codeListDictionary);
        await _datasetService.InsertDatasetsAsync(datasets);
        var valueLevels = await BuildSdtmValueLevelsAsync(parsedFiles, CurrentProject.Id);
        if (valueLevels.Count > 0)
            await _sqlSugar.Insertable(valueLevels).ExecuteCommandAsync();

        _messageService.Success($"Loaded {datasets.Count} dataset(s) and {valueLevels.Count} value level(s) from SDTM XPT files");
    }

    private async Task<(List<Dataset> Datasets, List<CodeList> CodeLists, List<ParsedSdtmFile> ParsedFiles)> BuildSdtmImportAsync(
        int projectId,
        List<ProjectFile> files)
    {
        List<Dataset> datasets = [];
        List<CodeList> codeLists = [];
        List<ParsedSdtmFile> parsedFiles = [];

        foreach (var file in files)
        {
            var parsedFile = await Task.Run(() => ParseStandardSdtmFile(file));
            if (parsedFile == null)
                continue;

            var dataset = await BuildDatasetAsync(parsedFile, projectId, codeLists);
            datasets.Add(dataset);
            parsedFiles.Add(parsedFile);
        }

        return (datasets, codeLists, parsedFiles);
    }

    private async Task<Dataset> BuildDatasetAsync(
        ParsedSdtmFile parsedFile,
        int projectId,
        List<CodeList> codeLists)
    {
        var name = parsedFile.Name;
        string queryName = name.StartsWith("SUPP")?"SUPPQUAL":name;
        var datasetStd = await _datasetService.GetStandardSdtmDatasetByNameAsync(queryName);
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
    private async Task<List<ValueLevel>> BuildSdtmValueLevelsAsync(
        IReadOnlyCollection<ParsedSdtmFile> parsedFiles,
        int projectId)
    {
        var datasetNames = parsedFiles.Select(o => o.Name).ToList();
        if (datasetNames.Count == 0)
            return [];

        var datasets = await _sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == projectId
                        && o.CdiscDataType == CdiscDataType.Sdtm
                        && datasetNames.Contains(o.Name))
            .OrderByDescending(o => o.Id)
            .ToListAsync();
        var datasetByName = datasets
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .GroupBy(o => o.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(o => o.Key, o => o.First(), StringComparer.OrdinalIgnoreCase);

        var variables = await _sqlSugar.Queryable<Variable>()
            .Where(o => o.ProjectId == projectId && o.CdiscDataType == CdiscDataType.Sdtm)
            .OrderByDescending(o => o.Id)
            .ToListAsync();
        var variableByDatasetAndName = variables
            .Where(o => !string.IsNullOrWhiteSpace(o.DatasetName) && !string.IsNullOrWhiteSpace(o.VariableName))
            .GroupBy(o => $"{o.DatasetName}.{o.VariableName}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(o => o.Key, o => o.First(), StringComparer.OrdinalIgnoreCase);

        var codeLists = await _sqlSugar.Queryable<CodeList>()
            .Where(o => o.ProjectId == projectId && o.CdiscDataType == CdiscDataType.Sdtm)
            .OrderByDescending(o => o.Id)
            .ToListAsync();
        var codeListByUniqueId = codeLists
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .GroupBy(o => o.UniqueId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(o => o.Key, o => o.First(), StringComparer.OrdinalIgnoreCase);

        List<ValueLevel> valueLevels = [];
        foreach (var parsedFile in parsedFiles)
        {
            if (!datasetByName.TryGetValue(parsedFile.Name, out var dataset))
                continue;

            var variablesByName = parsedFile.Variables
                .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(o => o.Key, o => o.Last(), StringComparer.OrdinalIgnoreCase);
            if (parsedFile.Name.StartsWith("SUPP", StringComparison.OrdinalIgnoreCase))
            {
                valueLevels.AddRange(BuildSuppValueLevels(
                    parsedFile,
                    dataset,
                    variablesByName,
                    variableByDatasetAndName,
                    codeListByUniqueId,
                    projectId));
                continue;
            }

            if (parsedFile.Name.Equals("TS", StringComparison.OrdinalIgnoreCase))
            {
                valueLevels.AddRange(await BuildTsValueLevelsAsync(
                    parsedFile,
                    dataset,
                    variablesByName,
                    variableByDatasetAndName,
                    codeListByUniqueId,
                    codeLists,
                    projectId));
                continue;
            }

            var testCodeName = $"{parsedFile.Name}TESTCD";
            if (!variablesByName.TryGetValue(testCodeName, out var testCodeVariable))
                continue;

            variablesByName.TryGetValue($"{parsedFile.Name}TEST", out var testVariable);
            variablesByName.TryGetValue($"{parsedFile.Name}CAT", out var categoryVariable);
            variablesByName.TryGetValue($"{parsedFile.Name}SCAT", out var subcategoryVariable);

            var contexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var recordCount = testCodeVariable.Entries?.Count ?? 0;
            for (var index = 0; index < recordCount; index++)
            {
                var testCode = GetSdtmEntry(testCodeVariable, index);
                if (string.IsNullOrWhiteSpace(testCode))
                    continue;

                var category = categoryVariable == null ? null : GetSdtmEntry(categoryVariable, index);
                var subcategory = subcategoryVariable == null ? null : GetSdtmEntry(subcategoryVariable, index);
                var whereClause = BuildSdtmValueLevelWhereClause(
                    testCodeName,
                    testCode,
                    categoryVariable?.Name,
                    category,
                    subcategoryVariable?.Name,
                    subcategory);
                contexts.TryAdd(whereClause, testVariable == null ? testCode : GetSdtmEntry(testVariable, index) ?? testCode);
            }

            var orderByVariable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var suffix in SdtmValueLevelVariableSuffixes)
            {
                var variableName = $"{parsedFile.Name}{suffix}";
                if (!variablesByName.TryGetValue(variableName, out _)
                    || !variableByDatasetAndName.TryGetValue($"{parsedFile.Name}.{variableName}", out var variable))
                {
                    continue;
                }

                foreach (var context in contexts)
                {
                    var order = orderByVariable.GetValueOrDefault(variableName) + 1;
                    orderByVariable[variableName] = order;
                    codeListByUniqueId.TryGetValue(variable.CodeListUniqueId ?? string.Empty, out var codeList);
                    valueLevels.Add(new ValueLevel
                    {
                        Order = order,
                        Dataset = parsedFile.Name,
                        DatasetId = dataset.Id,
                        Variable = variableName,
                        VariableId = variable.Id,
                        WhereClause = context.Key,
                        Label = context.Value,
                        Type = variable.DataType,
                        Length = variable.DataType == "datetime" ? null : variable.Length,
                        Digits = variable.SignificantDigits,
                        Format = variable.Format,
                        Mandatory = "No",
                        CodeListId = codeList?.Id ?? 0,
                        CodeListUniqueId = codeList?.UniqueId,
                        Origin = "Protocol",
                        Source = "Sponsor",
                        ProjectId = projectId,
                        CdiscDataType = CdiscDataType.Sdtm
                    });
                }
            }
        }

        return valueLevels;
    }

    private static List<ValueLevel> BuildSuppValueLevels(
        ParsedSdtmFile parsedFile,
        Dataset dataset,
        IReadOnlyDictionary<string, ParsedSdtmVariable> parsedVariablesByName,
        IReadOnlyDictionary<string, Variable> variablesByDatasetAndName,
        IReadOnlyDictionary<string, CodeList> codeListsByUniqueId,
        int projectId)
    {
        if (!parsedVariablesByName.TryGetValue("QNAM", out var qualifierNameVariable)
            || !parsedVariablesByName.TryGetValue("QVAL", out var qualifierValueVariable)
            || !variablesByDatasetAndName.TryGetValue($"{parsedFile.Name}.QVAL", out var valueVariable))
        {
            return [];
        }

        parsedVariablesByName.TryGetValue("QLABEL", out var qualifierLabelVariable);
        codeListsByUniqueId.TryGetValue(valueVariable.CodeListUniqueId ?? string.Empty, out var codeList);

        Dictionary<string, (string Label, List<string> Values)> qualifiers = new(StringComparer.OrdinalIgnoreCase);
        var recordCount = qualifierNameVariable.Entries?.Count ?? 0;
        for (var index = 0; index < recordCount; index++)
        {
            var qualifierName = GetSdtmEntry(qualifierNameVariable, index);
            if (string.IsNullOrWhiteSpace(qualifierName))
                continue;

            var qualifierLabel = qualifierLabelVariable == null
                ? qualifierName
                : GetSdtmEntry(qualifierLabelVariable, index) ?? qualifierName;
            var qualifierValue = GetSdtmEntry(qualifierValueVariable, index);
            if (!qualifiers.TryGetValue(qualifierName, out var qualifier))
                qualifier = (qualifierLabel, []);
            if (!string.IsNullOrWhiteSpace(qualifierValue))
                qualifier.Values.Add(qualifierValue);

            qualifiers[qualifierName] = qualifier;
        }

        return qualifiers
            .OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase)
            .Select((qualifier, index) =>
            {
                var metadata = InferValueLevelMetadata(qualifier.Value.Values);
                return new ValueLevel
                {
                    Order = index + 1,
                    Dataset = parsedFile.Name,
                    DatasetId = dataset.Id,
                    Variable = "QVAL",
                    VariableId = valueVariable.Id,
                    WhereClause = $"QNAM EQ {qualifier.Key}",
                    Label = qualifier.Value.Label,
                    Type = metadata.Type,
                    Length = metadata.Length,
                    Digits = metadata.Digits,
                    Format = metadata.Format,
                    Mandatory = "No",
                    CodeListId = codeList?.Id ?? 0,
                    CodeListUniqueId = codeList?.UniqueId,
                    Origin = "Collected",
                    Source = "Investigator",
                    ProjectId = projectId,
                    CdiscDataType = CdiscDataType.Sdtm
                };
            })
            .ToList();
    }

    private static ValueLevelMetadata InferValueLevelMetadata(IEnumerable<string> values)
    {
        var inferred = values.Select(InferValueMetadata).ToList();
        if (inferred.Count == 0)
            return new ValueLevelMetadata("text", null, null, null);

        var type = inferred.Any(o => o.Type == "text")
            ? "text"
            : inferred.Any(o => o.Type == "datetime")
                ? "datetime"
                : inferred.Any(o => o.Type == "float")
                    ? "float"
                    : "integer";
        var length = inferred.Where(o => o.Length.HasValue).Select(o => o.Length!.Value).DefaultIfEmpty().Max();
        var digits = inferred.Where(o => o.Digits.HasValue).Select(o => o.Digits!.Value).DefaultIfEmpty().Max();
        var formatWidth = inferred.Where(o => o.FormatWidth.HasValue).Select(o => o.FormatWidth!.Value).DefaultIfEmpty().Max();

        return new ValueLevelMetadata(
            type,
            type == "datetime" ? null : length == 0 ? null : length,
            digits == 0 ? null : digits,
            formatWidth == 0 ? null : $"${formatWidth}");
    }

    private static InferredValueMetadata InferValueMetadata(string value)
    {
        var normalized = value.Trim();
        if (Regex.IsMatch(normalized, "^\\d{4}-\\d{2}-\\d{2}(?:T\\d{2}:\\d{2}(?::\\d{2})?)?$"))
            return new InferredValueMetadata("datetime", null, null, normalized.Length);
        if (Regex.IsMatch(normalized, "^-?\\d+$"))
        {
            var length = normalized.TrimStart('-').Length;
            return new InferredValueMetadata("integer", length, null, length);
        }

        if (Regex.IsMatch(normalized, "^-?\\d+\\.\\d+$"))
        {
            var unsignedValue = normalized.TrimStart('-');
            var decimalPart = unsignedValue[(unsignedValue.IndexOf('.') + 1)..];
            var length = unsignedValue.Length;
            var digits = decimalPart.TrimEnd('0').Length;
            return new InferredValueMetadata("float", length, digits == 0 ? decimalPart.Length : digits, length);
        }

        return new InferredValueMetadata("text", normalized.Length, null, normalized.Length);
    }

    private sealed record ValueLevelMetadata(string Type, int? Length, int? Digits, string? Format);

    private sealed record InferredValueMetadata(string Type, int? Length, int? Digits, int? FormatWidth);

    private async Task<List<ValueLevel>> BuildTsValueLevelsAsync(
        ParsedSdtmFile parsedFile,
        Dataset dataset,
        IReadOnlyDictionary<string, ParsedSdtmVariable> parsedVariablesByName,
        IReadOnlyDictionary<string, Variable> variablesByDatasetAndName,
        IReadOnlyDictionary<string, CodeList> codeListsByUniqueId,
        IReadOnlyCollection<CodeList> codeLists,
        int projectId)
    {
        if (!parsedVariablesByName.TryGetValue("TSPARMCD", out var parameterCodeVariable))
            return [];

        parsedVariablesByName.TryGetValue("TSPARM", out var parameterVariable);
        parsedVariablesByName.TryGetValue("TSVCDREF", out var vocabularyReferenceVariable);
        var valueVariables = parsedVariablesByName.Values
            .Where(o => IsTsValueVariable(o.Name))
            .OrderBy(o => GetTsValueVariableOrder(o.Name))
            .ToList();
        if (valueVariables.Count == 0)
            return [];

        List<ValueLevel> valueLevels = [];
        HashSet<string> created = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> orderByVariable = new(StringComparer.OrdinalIgnoreCase);
        var recordCount = parameterCodeVariable.Entries?.Count ?? 0;
        for (var index = 0; index < recordCount; index++)
        {
            var parameterCode = GetSdtmEntry(parameterCodeVariable, index);
            if (string.IsNullOrWhiteSpace(parameterCode))
                continue;

            var label = parameterVariable == null
                ? parameterCode
                : GetSdtmEntry(parameterVariable, index) ?? parameterCode;
            var vocabularyReference = vocabularyReferenceVariable == null
                ? null
                : GetSdtmEntry(vocabularyReferenceVariable, index);

            foreach (var parsedValueVariable in valueVariables)
            {
                if (string.IsNullOrWhiteSpace(GetSdtmEntry(parsedValueVariable, index))
                    || !variablesByDatasetAndName.TryGetValue($"TS.{parsedValueVariable.Name}", out var variable))
                {
                    continue;
                }

                var key = $"{parsedValueVariable.Name}|{parameterCode}";
                if (!created.Add(key))
                    continue;

                CodeList? codeList = null;
                if (parsedValueVariable.Name.Equals("TSVAL", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(vocabularyReference, "CDISC CT", StringComparison.OrdinalIgnoreCase))
                {
                    var codeListRef = await GetCachedCodeListRefAsync($"TS.TSVAL.TSPARMCD.EQ.{parameterCode}");
                    if (codeListRef != null)
                    {
                        codeListsByUniqueId.TryGetValue(codeListRef.CodeListRef ?? string.Empty, out codeList);
                        codeList ??= codeLists.FirstOrDefault(o => o.Code == codeListRef.CodeListCode);
                    }
                }

                var valuesForParameter = Enumerable.Range(0, recordCount)
                    .Where(recordIndex => string.Equals(
                        GetSdtmEntry(parameterCodeVariable, recordIndex),
                        parameterCode,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(recordIndex => GetSdtmEntry(parsedValueVariable, recordIndex))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!);
                var metadata = InferValueLevelMetadata(valuesForParameter);

                var order = orderByVariable.GetValueOrDefault(parsedValueVariable.Name) + 1;
                orderByVariable[parsedValueVariable.Name] = order;
                valueLevels.Add(new ValueLevel
                {
                    Order = order,
                    Dataset = "TS",
                    DatasetId = dataset.Id,
                    Variable = parsedValueVariable.Name,
                    VariableId = variable.Id,
                    WhereClause = $"TSPARMCD EQ {parameterCode}",
                    Label = label,
                    Type = metadata.Type,
                    Length = metadata.Length,
                    Digits = metadata.Digits,
                    Format = metadata.Format,
                    Mandatory = "No",
                    CodeListId = codeList?.Id ?? 0,
                    CodeListUniqueId = codeList?.UniqueId,
                    Origin = "Protocol",
                    Source = "Sponsor",
                    ProjectId = projectId,
                    CdiscDataType = CdiscDataType.Sdtm
                });
            }
        }

        return valueLevels;
    }

    private static bool IsTsValueVariable(string variableName)
    {
        if (!variableName.StartsWith("TSVAL", StringComparison.OrdinalIgnoreCase))
            return false;

        return variableName.Length == 5 || variableName[5..].All(char.IsDigit);
    }

    private static int GetTsValueVariableOrder(string variableName)
    {
        return variableName.Length == 5 || !int.TryParse(variableName[5..], out var order) ? 0 : order;
    }

    private static readonly string[] SdtmValueLevelVariableSuffixes =
        ["ORRES", "ORRESU", "STRESN", "STRESC", "STRESU"];

    private static string? GetSdtmEntry(ParsedSdtmVariable variable, int index)
    {
        return variable.Entries is { Count: > 0 } && index < variable.Entries.Count
            ? variable.Entries[index]?.Trim()
            : null;
    }

    private static string BuildSdtmValueLevelWhereClause(
        string testCodeName,
        string testCode,
        string? categoryName,
        string? category,
        string? subcategoryName,
        string? subcategory)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(categoryName) && !string.IsNullOrWhiteSpace(category))
            parts.Add($"{categoryName} EQ {category}");
        if (!string.IsNullOrWhiteSpace(subcategoryName) && !string.IsNullOrWhiteSpace(subcategory))
            parts.Add($"{subcategoryName} EQ {subcategory}");
        parts.Add($"{testCodeName} EQ {testCode}");
        return string.Join(" and ", parts);
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
            catch (Exception)
            {
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
            allRecords.Count > 0,
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

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        CurrentProject = _currentProjectService.CurrentProject;
        LoadFiles();
        return Task.CompletedTask;
    }

    public override Task<bool> IsNavigationTargetAsync(NavigationContext navigationContext)
    {
        return Task.FromResult(true);
    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext) => Task.CompletedTask;

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
