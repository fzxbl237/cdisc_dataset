using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using PatChes.Controls.DataGrid;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Models.Settings;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using LiteDB;
using MapsterMapper;
using P21.Validator.Api.Options;
using P21.Validator.Data;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using SqlSugar;

namespace PatChes.ViewModels.Dialogs;

public partial class EditTermsViewModel : ObservableObject, IDialogHostAware
{
    private readonly ICodeListService _codeListService;
    private readonly ITermService _termService;
    private readonly IVariableService _variableService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMessageService _messageService;
    private readonly IValidator<TermDto> _validator;
    private readonly IMapper _mapper;
    private readonly ISqlSugarClient _sqlSugar;
    private readonly ILiteDatabase _liteDatabase;
    private readonly ILiteCollection<ProjectFile> _projectFiles;
    private CodeListDto _codeList = new();

    public string? DialogHostName { get; set; } = "Root";
    public AvaloniaList<TermDto> Terms { get; } = [];
    public AvaloniaList<VariableOption> Variables { get; } = [];

    [ObservableProperty] private string _title = "Edit Terms";
    [ObservableProperty] private VariableOption? _selectedVariable;
    [ObservableProperty] private TermOptionsAsyncLoader _termOptionsAsyncLoader;

    public EditTermsViewModel(
        ICodeListService codeListService,
        ITermService termService,
        IVariableService variableService,
        ICurrentProjectService currentProjectService,
        IMessageService messageService,
        IValidator<TermDto> validator,
        IMapper mapper,
        ISqlSugarClient sqlSugar,
        ILiteDatabase liteDatabase)
    {
        _codeListService = codeListService;
        _termService = termService;
        _variableService = variableService;
        _currentProjectService = currentProjectService;
        _messageService = messageService;
        _validator = validator;
        _mapper = mapper;
        _sqlSugar = sqlSugar;
        _liteDatabase = liteDatabase;
        _projectFiles = liteDatabase.GetCollection<ProjectFile>("project_files");
        TermOptionsAsyncLoader = new TermOptionsAsyncLoader(sqlSugar);
        Terms.CollectionChanged += OnTermsCollectionChanged;
    }

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        if (parameters?.TryGetValue<CodeListDto>("Model", out var model) != true || model == null)
        {
            DialogHost.Close(DialogHostName, new DialogHostResult(DialogButtonResult.Cancel));
            return;
        }

        _codeList = _mapper.Map<CodeListDto>(model);
        Title = $"Edit Terms: {_codeList.UniqueId}";

        var references = await _codeListService.GetAllCodeListReferencesAsync();
        TermOptionsAsyncLoader.CodeListReference = references.FirstOrDefault(reference =>
            string.Equals(reference.CodeListRef, _codeList.UniqueId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reference.CodeListRef?.Split('.').LastOrDefault(), _codeList.UniqueId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reference.CodeListCode, _codeList.Code, StringComparison.OrdinalIgnoreCase));

        var terms = await _termService.GetTermDtosByCodeListIdAsync(_codeList.Id) ?? [];
        Terms.AddRange(terms);
        //SetTerms(_mapper.Map<List<TermDto>>(terms.OrderBy(term => term.Order)));
        UpdateTermsDuplicate();
        UpdateDecodedValueConsistent();
        await ValidateTermsAsync();
        await LoadVariablesAsync();
    }

    private void OnTermsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (TermDto term in e.OldItems)
                DetachTerm(term);
        if (e.NewItems != null)
            foreach (TermDto term in e.NewItems)
                AttachTerm(term);

        UpdateTermsOrder();
        UpdateTermsDuplicate();
        UpdateDecodedValueConsistent();
        foreach (var term in Terms)
            ValidateTermDtoAsync(term).AwaitWithOpt();
    }

    private async Task LoadVariablesAsync()
    {
        var variables = await _variableService.GetAllVariableDtosAsync();
        Variables.Clear();
        Variables.AddRange(variables
            .Where(variable => !string.IsNullOrWhiteSpace(variable.DatasetName)
                               && !string.IsNullOrWhiteSpace(variable.VariableName))
            .OrderBy(variable => variable.DatasetName)
            .ThenBy(variable => variable.VariableName)
            .Select(variable => new VariableOption
            {
                Header = $"{variable.DatasetName}.{variable.VariableName}",
                Content = $"{variable.DatasetName}.{variable.VariableName} {variable.Label}",
                Variable = variable
            }));
        SelectedVariable = null;
    }

    [RelayCommand]
    private async Task AddTermsFromXptAsync()
    {
        var variable = SelectedVariable?.Variable;
        if (variable == null)
        {
            _messageService.Warning("Please select a variable first.");
            return;
        }

        var datasetName = variable.DatasetName;
        var variableName = variable.VariableName;
        if (string.IsNullOrWhiteSpace(datasetName) || string.IsNullOrWhiteSpace(variableName))
            return;

        var projectId = _currentProjectService.CurrentProject?.Id ?? 0;
        var normalizedDatasetName = Path.GetFileNameWithoutExtension(datasetName.Trim());
        var projectFile = _projectFiles.Query()
            .Where(file => file.ProjectId == projectId && file.FileType == ProjectFileType.Sdtm)
            .ToList()
            .FirstOrDefault(file => string.Equals(
                Path.GetFileNameWithoutExtension(file.FileName.Trim()),
                normalizedDatasetName,
                StringComparison.OrdinalIgnoreCase));
        if (projectFile == null)
        {
            _messageService.Error($"SDTM XPT file for {normalizedDatasetName} was not found in the current project.");
            return;
        }

        if (!_liteDatabase.FileStorage.Exists(projectFile.StorageId.ToString()))
        {
            _messageService.Error($"SDTM XPT file {projectFile.FileName} is registered but its stored content is missing.");
            return;
        }

        var values = await Task.Run(() => ReadDistinctValues(projectFile, variableName));
        if (values.Count == 0)
        {
            _messageService.Warning($"No non-empty values were found for {datasetName}.{variableName}.");
            return;
        }

        var codeListRef = TermOptionsAsyncLoader.CodeListReference?.CodeListRef;
        var standardTerms = string.IsNullOrWhiteSpace(codeListRef)
            ? []
            : await _codeListService.GetCodeListTermsAsync(codeListRef);
        var standardTermsByValue = standardTerms
            .Where(term => !string.IsNullOrWhiteSpace(term.CodeValue))
            .GroupBy(term => term.CodeValue!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var combinedTerms = Terms.ToList();
        combinedTerms.AddRange(values.Select(value =>
        {
            standardTermsByValue.TryGetValue(value, out var standardTerm);
            return new TermDto
            {
                Name = value,
                Code = standardTerm?.Code ?? string.Empty,
                DecodedValue = standardTerm?.DecodedValue ?? string.Empty,
                CodeListId = _codeList.Id,
                CodeListUniqueId = _codeList.UniqueId,
                CodeList = _mapper.Map<CodeList>(_codeList)
            };
        }));

        var mergedTerms = combinedTerms
            .GroupBy(term => (term.Name ?? string.Empty, term.Code ?? string.Empty))
            .Select(group => group.First())
            .ToList();
        var addedCount = mergedTerms.Count - Terms.Count;
        var removedDuplicateCount = combinedTerms.Count - mergedTerms.Count;

        SetTerms(mergedTerms);
        UpdateTermsOrder();
        UpdateTermsDuplicate();
        UpdateDecodedValueConsistent();
        await ValidateTermsAsync();
        _messageService.Success(
            $"Loaded {values.Count} distinct value(s); added {Math.Max(0, addedCount)} term(s) and removed {removedDuplicateCount} duplicate(s).");
    }

    private List<string> ReadDistinctValues(ProjectFile projectFile, string variableName)
    {
        var storedFile = _liteDatabase.FileStorage.FindById(projectFile.StorageId.ToString());
        if (storedFile == null)
            return [];

        using var memoryStream = new MemoryStream();
        storedFile.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var validationOptions = ValidationOptions.CreateBuilder().Build();
        var factory = new DataEntryFactory(validationOptions);
        var options = SourceOptions.builder()
            .WithName(Path.GetFileNameWithoutExtension(projectFile.FileName).ToUpperInvariant())
            .WithMemoryStream(memoryStream)
            .WithType(SourceOptions.StandardTypes.SasTransport)
            .Build();

        using var dataSource = new SasTransportDataSource(options, factory);
        var sourceVariableName = dataSource.GetVariables()
            .FirstOrDefault(name => string.Equals(name, variableName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(sourceVariableName))
            return [];

        var values = new HashSet<string>(StringComparer.Ordinal);
        while (dataSource.HasRecords())
        {
            var records = dataSource.GetRecords();
            if (records.Count == 0)
                break;

            foreach (var record in records)
            {
                var entry = record.GetValue(sourceVariableName);
                if (entry?.HasValue != true)
                    continue;

                var value = entry.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }
        }

        return values.OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    private void AttachTerm(TermDto term) => term.PropertyChanged += TermOnPropertyChanged;
    private void DetachTerm(TermDto term) => term.PropertyChanged -= TermOnPropertyChanged;

    private void TermOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TermDto term || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        var duplicateFlagProperty = e.PropertyName switch
        {
            nameof(TermDto.IsNameDuplicate) => nameof(TermDto.Name),
            _ => null
        };

        if (duplicateFlagProperty != null)
        {
            Observable.StartAsync(() => _validator.ValidateDtoAsync(term, duplicateFlagProperty));
            return;
        }

        if (e.PropertyName is not (
                nameof(TermDto.Name) or
                nameof(TermDto.DecodedValue) or
                nameof(TermDto.Order) or
                nameof(TermDto.CodeListUniqueId) or 
                nameof(TermDto.DecodedValueConsistent)))
        {
            return;
        }

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(TermDto.Name):
                {
                    var codeListRef = TermOptionsAsyncLoader.CodeListReference?.CodeListRef;
                    var standardTerm = string.IsNullOrWhiteSpace(codeListRef)
                        ? null
                        : _sqlSugar.AsTenant().QueryableWithAttr<CodeListTerm>()
                            .AsWithAttr()
                            .Where(item => item.CodeListRef == codeListRef && item.CodeValue == term.Name)
                            .First();
                    term.Code = standardTerm?.Code ?? string.Empty;
                    term.DecodedValue = standardTerm?.DecodedValue ?? string.Empty;
                    UpdateTermsDuplicate();
                    await _validator.ValidateDtoAsync(term, nameof(TermDto.Name));
                    break;
                }
                case nameof(TermDto.DecodedValue):
                    UpdateDecodedValueConsistent();
                    await _validator.ValidateDtoAsync(term, nameof(TermDto.DecodedValue));
                    break;
                case nameof(TermDto.Order):
                    await _validator.ValidateDtoAsync(term, nameof(TermDto.Order));
                    break;
                case nameof(TermDto.CodeListUniqueId):
                    await _validator.ValidateDtoAsync(term, nameof(TermDto.CodeListUniqueId));
                    break;
                case nameof(TermDto.DecodedValueConsistent):
                    await _validator.ValidateDtoAsync(term, nameof(TermDto.DecodedValue));
                    break; 
            }
        });
    }

    [RelayCommand]
    private async Task AddTerm()
    {
        var term = CreateTerm();
        Terms.Add(term);
        await ValidateTermDtoAsync(term);
    }

    [RelayCommand]
    private async Task InsertTermAbove(TermDto? term)
    {
        var index = term == null ? -1 : Terms.IndexOf(term);
        if (index < 0)
            return;

        var newTerm = CreateTerm();
        Terms.Insert(index, newTerm);
        await ValidateTermDtoAsync(newTerm);
    }

    [RelayCommand]
    private async Task InsertTermBelow(TermDto? term)
    {
        var index = term == null ? -1 : Terms.IndexOf(term);
        if (index < 0)
            return;

        var newTerm = CreateTerm();
        Terms.Insert(index + 1, newTerm);
        await ValidateTermDtoAsync(newTerm);
    }

    [RelayCommand]
    private void Delete(TermDto? term)
    {
        if (term != null)
            Terms.Remove(term);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var selectedTerms = Terms.Where(term => term.IsSelected).ToList();
        if (selectedTerms.Count == 0)
        {
            _messageService.Info("Please select at least one term to delete.");
            return;
        }

        foreach (var term in selectedTerms)
            Terms.Remove(term);
    }

    [RelayCommand]
    private async Task Save()
    {
        await ValidateTermsAsync();
        _codeList.Terms = _mapper.Map<List<Term>>(Terms);
        DialogHost.Close(DialogHostName, new DialogHostResult(DialogButtonResult.Yes, new DialogParameters
        {
            { "Model", _codeList }
        }));
    }

    [RelayCommand]
    private void Cancel() => DialogHost.Close(DialogHostName, new DialogHostResult(DialogButtonResult.Cancel));

    private async Task ValidateTermDtoAsync(TermDto term)
    {
        await _validator.ValidateDtoAsync(term);
    }

    private Task ValidateTermsAsync()
    {
        return Task.WhenAll(Terms.Select(ValidateTermDtoAsync));
    }

    private TermDto CreateTerm() => new()
    {
        CodeListId = _codeList.Id,
        CodeListUniqueId = _codeList.UniqueId,
        CodeList = _mapper.Map<CodeList>(_codeList)
    };

    private void SetTerms(IEnumerable<TermDto> terms)
    {
        foreach (var term in Terms.ToList())
            DetachTerm(term);
        Terms.Clear();
        Terms.AddRange(terms);
    }

    private void UpdateTermsOrder()
    {
        for (var index = 0; index < Terms.Count; index++)
            Terms[index].Order = index + 1;
    }

    private void UpdateTermsDuplicate()
    {
        foreach (var term in Terms)
            term.IsNameDuplicate = false;

        Terms.MarkDuplicates(
            term => term.Name??string.Empty,
            (term, isDuplicate) => term.IsNameDuplicate = isDuplicate,
            name => !string.IsNullOrWhiteSpace(name));
    }

    private void UpdateDecodedValueConsistent()
    {
        var populatedCount = Terms.Count(term => !string.IsNullOrWhiteSpace(term.DecodedValue));
        var consistent = populatedCount == 0 || populatedCount == Terms.Count;
        foreach (var term in Terms)
            term.DecodedValueConsistent = consistent;
    }
}
