using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
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
    private bool _isLoading;

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

        _sourceCache.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(_ =>
            {
                if (!HasChanges)
                    HasChanges = true;
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.UniqueId, false)
            .Subscribe(change =>
            {
                MarkDuplicates();
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(MethodDto.UniqueId));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Name, false)
            .Subscribe(change =>
            {
                MarkDuplicates();
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(MethodDto.Name));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Type, false)
            .Subscribe(change =>
            {
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(MethodDto.Type));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Description, false)
            .Subscribe(change =>
            {
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(change.Sender, nameof(MethodDto.Description));
                    _sourceCache.AddOrUpdate(change.Sender);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.DocumentUniqueId, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                if (!string.IsNullOrWhiteSpace(changeSender.DocumentUniqueId) && _frozenDocumentDictionary!=null)
                {
                    _frozenDocumentDictionary.TryGetValue(changeSender.DocumentUniqueId, out Document? document);
                    if (document != null)
                    {
                        changeSender.Document = document;
                        changeSender.DocumentId = document.Id;
                    }
                }
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "Pages");
                    await _validator.ValidateDtoAsync(changeSender, nameof(MethodDto.DocumentUniqueId));
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Pages, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "Pages");
                    await _validator.ValidateDtoAsync(changeSender, "DocumentUniqueId");
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.HasNameDuplicate, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "Name");
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.HasUniqueIdDuplicate, false)
            .Subscribe((change) =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender, "UniqueId");
                    _sourceCache.AddOrUpdate(changeSender);
                });
            });
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
        var list = await _methodService.GetAllMethodDtosAsync();

        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(list);
        });
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
            { "Title", "新增 Method" },
            { "ProjectId", _currentProjectService.CurrentProject.Id },
            { "CdiscDataType", CdiscDataType },
            { "Model", dto }
        };

        var result = await _dialogHostService.ShowDialog("MethodDialog", parameters);
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Model"))
            return;

        var method = result.Parameters.GetValue<MethodDto>("Model");
        await _validator.ValidateDtoAsync(method);
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
            { "Title", "编辑 Method" },
            { "ProjectId", _currentProjectService.CurrentProject.Id },
            { "CdiscDataType", CdiscDataType },
            { "Model", methodDto }
        };

        var result = await _dialogHostService.ShowDialog("MethodDialog", parameters);
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Model"))
            return;

        var editedMethod = result.Parameters.GetValue<MethodDto>("Model");
        await _validator.ValidateDtoAsync(editedMethod);
        _sourceCache.AddOrUpdate(editedMethod);
        MarkDuplicates();
        HasChanges = true;
    }

    [RelayCommand]
    private async Task Delete(MethodDto methodDto)
    {
        await _methodService.DeleteMethodAsync(methodDto);
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

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        CdiscDataType = cdiscDataType;
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
}
