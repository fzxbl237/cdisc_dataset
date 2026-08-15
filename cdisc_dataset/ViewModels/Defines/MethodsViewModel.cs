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
    private readonly cdisc_dataset.Services.IDialogService _dialogService;
    private readonly IMapper _mapper;
    private readonly IValidator<MethodDto> _validator;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isErrorOnly;

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
        cdisc_dataset.Services.IDialogService dialogService,
        IMapper mapper,
        IValidator<MethodDto> validator,
        ILookupStore lookupStore)
    {
        _messageService = messageService;
        _methodService = methodService;
        _documentService = documentService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _mapper = mapper;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(_ => BuildFilter());

        _sourceCache.Connect()
            .AutoRefresh(o => o.HasErrors)
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _methods, SortExpressionComparer<MethodDto>.Ascending(o => o.UniqueId ?? string.Empty)
                .ThenByAscending(o=>o.Name?? string.Empty))
            .DisposeMany()
            .Subscribe();

        lookupStore.Documents
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildDocumentLookups);

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
                    ApplyDocument(methodDto, methodDto.DocumentUniqueId);
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Pages),nameof(MethodDto.DocumentUniqueId));
                    break;
                case nameof(MethodDto.Pages):
                    await _validator.ValidateDtoAsync(methodDto, nameof(MethodDto.Pages),nameof(MethodDto.DocumentUniqueId));
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

    partial void OnIsErrorOnlyChanged(bool value) => _sourceCache.Refresh();

    private Func<MethodDto, bool> BuildFilter()
    {
        var searchFilter = SearchFilterExtensions.BuildSearchFilter<MethodDto>(
            SearchText,
            x => x.UniqueId,
            x => x.Name,
            x => x.Type,
            x => x.Description,
            x => x.ExpressionContext,
            x => x.ExpressionCode,
            x => x.Pages,
            x => x.DocumentUniqueId);
        return method => (!IsErrorOnly || method.HasErrors) && searchFilter(method);
    }
    
    public async Task LoadMethods()
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

    private void RebuildDocumentLookups(IReadOnlyCollection<Document> documents)
    {
        DocumentOptions.Clear();
        DocumentOptions.AddRange(documents.Select(document => new DocumentAutoCompleteOption
        {
            Header = $"{document.UniqueId} {document.Title}",
            Content = document.UniqueId,
            Document = document
        }));

        _frozenDocumentDictionary = documents
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
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
    private async Task AddDocumentAsync(MethodDto methodDto)
    {
        var result = await _dialogService.ShowAddDocumentModelAsync(new DocumentDto());
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var documentDto))
            return;

        var inserted = await _documentService.InsertDocumentAsync(documentDto);
        methodDto.Document = _mapper.Map<Document>(inserted);
        methodDto.DocumentId = inserted.Id;
        methodDto.DocumentUniqueId = inserted.UniqueId;
    }

    [RelayCommand]
    private async Task EditDocumentAsync(MethodDto methodDto)
    {
        if (methodDto.Document == null)
            return;

        var result = await _dialogService.ShowEditDocumentModelAsync(_mapper.Map<DocumentDto>(methodDto.Document));
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var documentDto))
            return;

        await _documentService.UpdateDocumentAsync(documentDto);
        methodDto.Document = _mapper.Map<Document>(documentDto);
        methodDto.DocumentId = documentDto.Id;
        methodDto.DocumentUniqueId = documentDto.UniqueId;
    }

    private void ApplyDocument(MethodDto methodDto, string? documentUniqueId)
    {
        if (documentUniqueId == methodDto.DocumentUniqueId) return;
        if (string.IsNullOrWhiteSpace(documentUniqueId) || _frozenDocumentDictionary == null ||
            !_frozenDocumentDictionary.TryGetValue(documentUniqueId, out var document))
        {
            methodDto.Document = null;
            methodDto.DocumentId = 0;
            return;
        }

        methodDto.Document = document;
        methodDto.DocumentId = document.Id;
        methodDto.DocumentUniqueId = document.UniqueId;
    }

    [RelayCommand]
    private async Task AddMethodAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var dto = new MethodDto
        {
            ProjectId = _currentProjectService.CurrentProject.Id,
            CdiscDataType = _currentProjectService.CdiscDataType,
        };

        var result = await _dialogService.ShowAddMethodModelAsync(dto);
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<MethodDto>("Model", out var method))
            return;

        await _validator.ValidateDtoAsync(method);
        RegisterMethodDtoPropertyChanged(method);
        _sourceCache.AddOrUpdate(method);
        MarkDuplicates();
        await _methodService.InsertMethodAsync(method);
        //HasChanges = true;
    }

    [RelayCommand]
    private async Task EditMethodAsync(MethodDto methodDto)
    {
        if (_currentProjectService.CurrentProject == null)
            return;
        
        var result = await _dialogService.ShowEditMethodModelAsync(_mapper.Map<MethodDto>(methodDto));
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<MethodDto>("Model", out var editedMethod))
            return;

        await _validator.ValidateDtoAsync(editedMethod);
        _sourceCache.AddOrUpdate(editedMethod);
        await _methodService.UpdateMethodAsync(editedMethod);
        MarkDuplicates();
        //HasChanges = true;
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
        _messageService.Success("Method deleted successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        await _methodService.SaveMethodsAsync(Methods.ToList());
        HasChanges = false;
        _messageService.Success("Methods saved successfully.");
        if (_currentProjectService.CurrentProject != null)
            await LoadMethods();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        await LoadMethods();
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await LoadMethods();
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
