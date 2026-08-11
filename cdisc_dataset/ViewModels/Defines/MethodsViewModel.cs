using AsyncNavigation;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using Prism.Dialogs;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class MethodsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IMethodService _methodService;
    private readonly IDocumentService _documentService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMapper _mapper;
    private readonly IValidator<MethodDto> _validator;

    [ObservableProperty]
    private CdiscDataType _cdiscDataType;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private AvaloniaList<IAutoCompleteOption> _documentOptions = [];
    
    [ObservableProperty] private AvaloniaList<string> _types = ["Computation", "Imputation"];
    
    private FrozenDictionary<string,Document>? _frozenDocumentDictionary;

    private readonly SourceCache<MethodDto, int> _sourceCache = new(o => o.Id);
    private readonly ReadOnlyObservableCollection<MethodDto> _methods;
    public ReadOnlyObservableCollection<MethodDto> Methods => _methods;

    public MethodsViewModel(
        IMessageService messageService,
        IMethodService methodService,
        IDocumentService documentService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IMapper mapper,
        IValidator<MethodDto> validator)
    {
        _messageService = messageService;
        _methodService = methodService;
        _documentService = documentService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _mapper = mapper;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);

        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _methods, SortExpressionComparer<MethodDto>.Ascending(o => o.UniqueId ?? string.Empty)
                .ThenByAscending(o=>o.Name?? string.Empty))
            .DisposeMany()
            .Subscribe();

    }

    private void MethodDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MethodDto methodDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(MethodDto.HasChanged))
            return;

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(MethodDto.UniqueId):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.UniqueId));
                    break;
                case nameof(MethodDto.Name):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Name));
                    break;
                case nameof(MethodDto.Type):
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Type));
                    break;
                case nameof(MethodDto.Description):
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Description));
                    break;
                case nameof(MethodDto.DocumentUniqueId):
                    if (!string.IsNullOrWhiteSpace(methodDto.DocumentUniqueId) && _frozenDocumentDictionary != null &&
                        _frozenDocumentDictionary.TryGetValue(methodDto.DocumentUniqueId, out var document))
                    {
                        methodDto.Document = document;
                        methodDto.DocumentId = document.Id;
                    }
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Pages));
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.DocumentUniqueId));
                    break;
                case nameof(MethodDto.Pages):
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Pages));
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.DocumentUniqueId));
                    break;
                case nameof(MethodDto.HasNameDuplicate):
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Name));
                    break;
                case nameof(MethodDto.HasUniqueIdDuplicate):
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.UniqueId));
                    break;
                default:
                    return;
            }

            _sourceCache.AddOrUpdate(methodDto);
        });

        methodDto.HasChanged = true;
        HasChanges = true;
    }

    private void RegisterMethodDtoPropertyChanged(MethodDto methodDto)
    {
        methodDto.PropertyChanged += MethodDtoOnPropertyChanged;
    }

    private void UnregisterMethodDtoPropertyChanged(MethodDto methodDto)
    {
        methodDto.PropertyChanged -= MethodDtoOnPropertyChanged;
    }

    private static Func<MethodDto, bool> BuildFilter(string? searchText)
        => SearchFilterExtensions.BuildSearchFilter<MethodDto>(
            searchText,
            x => x.UniqueId,
            x => x.Name,
            x => x.Type,
            x => x.Description,
            x => x.ExpressionContext,
            x => x.ExpressionCode,
            x => x.Pages,
            x => x.DocumentUniqueId);
    
    public async Task LoadMethods(int projectId, CdiscDataType cdiscDataType)
    {
        foreach (var methodDto in _sourceCache.Items)
            UnregisterMethodDtoPropertyChanged(methodDto);

        var list = await _methodService.GetAllMethodDtosAsync();
        foreach (var methodDto in list)
        {
            await _validator.ValidateDtoAsync(methodDto);
            RegisterMethodDtoPropertyChanged(methodDto);
        }

        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
        MarkDuplicates();
        HasChanges = false;
    }

    public async Task LoadDocuments(int projectId, CdiscDataType cdiscDataType)
    {
        var list = await _documentService.GetAllDocumentsWithoutErorrAsync();
        List<IAutoCompleteOption> res = [];
        foreach (var document in list)
        {
            var documentAutoCompleteOption = new DocumentAutoCompleteOption()
            {
                Header = $"{document.UniqueId} {document.Title}",
                Content = document.UniqueId,
                Document = document
            };
            res.Add(documentAutoCompleteOption);
        }
        DocumentOptions.Clear();
        DocumentOptions.AddRange(res);
        _frozenDocumentDictionary=list.Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);
    }
    

    private void MarkDuplicates()
    {
        _sourceCache.Items.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (method, isDuplicate) => method.HasUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        _sourceCache.Items.MarkDuplicates(
            o => o.Name ?? string.Empty,
            (method, isDuplicate) => method.HasNameDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    [RelayCommand]
    private async Task AddMethod()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var dto = new MethodDto
        {
            ProjectId = _currentProjectService.CurrentProject.Id,
            CdiscDataType = CdiscDataType,
        };

        var parameters = new DialogParameters
        {
            { "Title", "???? Method" },
            { "Model", dto }
        };

        var result = await _dialogHostService.ShowDialogAsync("MethodDialog", parameters);
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Model"))
            return;

        var method = result.Parameters.GetValue<MethodDto>("Model");
        await _validator.ValidateDtoAsync(method);
        RegisterMethodDtoPropertyChanged(method);
        _sourceCache.AddOrUpdate(method);
        MarkDuplicates();
        //await _methodService.InsertMethodAsync(method);
        HasChanges = true;
    }

    [RelayCommand]
    private async Task EditMethod(MethodDto methodDto)
    {
        if (_currentProjectService.CurrentProject == null)
            return;
        
        var parameters = new DialogParameters
        {
            { "Title", "?? Method" },
            { "Model", methodDto }
        };

        var result = await _dialogHostService.ShowDialogAsync("MethodDialog", parameters);
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Model"))
            return;

        var editedMethod = result.Parameters.GetValue<MethodDto>("Model");
        await _validator.ValidateDtoAsync(editedMethod);
        _sourceCache.AddOrUpdate(editedMethod);
        MarkDuplicates();
        HasChanges = true;
    }

    [RelayCommand]
    private async Task DeleteAsync(MethodDto methodDto)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Method" },
            { "Message", $"Are you sure you want to delete method {methodDto.Name}?" }
        });
        if (result.Result != ButtonResult.OK)
            return;

        await _methodService.DeleteMethodAsync(methodDto);
        UnregisterMethodDtoPropertyChanged(methodDto);
        _sourceCache.Remove(methodDto);
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success("Delete Success");
    }

    [RelayCommand]
    private async Task Save()
    {
        await _methodService.SaveMethodsAsync(Methods.ToList());
        HasChanges = false;
        _messageService.Success("Methods Save Success");
        if (_currentProjectService.CurrentProject != null)
            await LoadMethods(_currentProjectService.CurrentProject.Id, CdiscDataType);
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        await LoadMethods(_currentProjectService.CurrentProject.Id, CdiscDataType);
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        CdiscDataType = _currentProjectService.CdiscDataType;
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await LoadMethods(_currentProjectService.CurrentProject.Id, CdiscDataType);
        await LoadDocuments(_currentProjectService.CurrentProject.Id, CdiscDataType);
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var methodDto in _sourceCache.Items)
            UnregisterMethodDtoPropertyChanged(methodDto);

        return Task.CompletedTask;
    }
}
