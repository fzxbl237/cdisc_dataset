using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Controls.DataGrid;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LiteDB;
using MapsterMapper;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using SqlSugar;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class CodeListDialogViewModel: ObservableObject, IDialogHostAware
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly ICommentService _commentService;
    private readonly IVariableService _variableService;
    private readonly ICodeListService _codeListService;
    private readonly IMessageService _messageService;
    private readonly ILiteDatabase _liteDatabase;
    private readonly IMapper _mapper;
    public string? DialogHostName { get; set; } = "Root";
    
    //public AvaloniaList<Comment> Comments { get; set; } = [];
    
    public AvaloniaList<string> Types { get; set; } = ["text", "integer", "float","datetime","date","time",
        "partialDate","partialTime","partialDateTime","incompleteDatetime","durationDatetime","intervalDatetime"];

    public AvaloniaList<string?> Terminologies  { get; set; } = [];
    
    [ObservableProperty]
    private AvaloniaList<CodeListOption> _codeLists = [];
    
    [ObservableProperty] private CodeListOption? _selectedCodeList;
    
    [ObservableProperty]
    private AvaloniaList<ISelectOption> _comments = [];
    
    [ObservableProperty]
    private ISelectOption? _selectedComment;
    
    public AvaloniaList<VariableOption> Variables { get; } = [];

    [ObservableProperty] private VariableOption? _selectedVariable;
    [ObservableProperty] private string _selectedVariableDisplay = string.Empty;
    [ObservableProperty] private bool _isVariableSelectionVisible;

    [ObservableProperty] private CodeListDto _codeListDto = new();

    [ObservableProperty] private bool _isInEditMode;
    
    [ObservableProperty] private string? _display;
    [ObservableProperty] private string _dialogTitle = "Add CodeList";
    
    public CodeListDialogViewModel(
        ISqlSugarClient sqlSugar,
        ICurrentProjectService currentProjectService,
        ICommentService commentService,
        IVariableService variableService,
        ICodeListService codeListService,
        IMessageService messageService,
        ILiteDatabase liteDatabase,
        IMapper mapper)
    {
        _sqlSugar = sqlSugar;
        _currentProjectService = currentProjectService;
        _commentService = commentService;
        _variableService = variableService;
        _codeListService = codeListService;
        _messageService = messageService;
        _liteDatabase = liteDatabase;
        _mapper = mapper;
    }

    private void CodeListDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CodeListDto.Terminology) && string.IsNullOrWhiteSpace(CodeListDto.Terminology))
        {
            CodeLists.Clear();
            SelectedCodeList = null;
        }
    }

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        CodeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        SelectedVariable = null;
        Display = "Select CodeList";
        IsInEditMode = parameters.TryGetValue<CodeListDto>("Model", out var model);
        DialogTitle = IsInEditMode ? "Edit CodeList" : "Add CodeList";
        IsVariableSelectionVisible = !IsInEditMode &&
                                     (parameters.ContainsKey("Variable") ||
                                      (parameters.TryGetValue<bool>("SelectVariable", out var selectVariable) && selectVariable));


        Terminologies.Clear();
        var list = _sqlSugar.Queryable<CodeListStd>().Select(o => o.Terminology).Distinct().ToList();
        Terminologies.Add(" ");
        Terminologies.AddRange(list);
        if (IsInEditMode && model != null)
        {
            CodeListDto = model;
            Display = $"Edit codelist: {model.UniqueId}";
        }
        else if (IsVariableSelectionVisible)
        {
            parameters.TryGetValue<VariableDto>("Variable", out var defaultVariable);
            await LoadVariablesAsync(defaultVariable);
        }
        if (IsInEditMode && !string.IsNullOrWhiteSpace(CodeListDto.Terminology))
        {
            CodeListDto.Terminology = Terminologies.FirstOrDefault(o =>
                string.Equals(o, CodeListDto.Terminology, StringComparison.OrdinalIgnoreCase))
                ?? CodeListDto.Terminology;
        }
        await LoadComments();
        if (IsInEditMode && CodeListDto.CommentId > 0)
        {
            SelectedComment = Comments.FirstOrDefault(o => o.Content is Comment comment && comment.Id == CodeListDto.CommentId);
        }

        if (!IsInEditMode && Terminologies.Count >= 2)
        {
            CodeListDto.Terminology = Terminologies[1];
        }

        CodeListDto.PropertyChanged += CodeListDtoOnPropertyChanged;
        if (string.IsNullOrWhiteSpace(CodeListDto.Type))
            CodeListDto.Type = Types[0];
    }

    private async Task LoadVariablesAsync(VariableDto? defaultVariable)
    {
        var variables = await _variableService.GetAllVariableDtosAsync();
        Variables.Clear();
        Variables.AddRange(variables
            .OrderBy(o => o.DatasetName)
            .ThenBy(o => o.VariableName)
            .Select(o => new VariableOption
            {
                Header = $"{o.DatasetName}.{o.VariableName}",
                Content = $"{o.DatasetName}.{o.VariableName} {o.Label}",
                Variable = o
            }));

        if (defaultVariable == null)
        {
            await LoadAllCodeListsAsync();
            return;
        }

        SelectedVariable = Variables.FirstOrDefault(o => o.Variable.Id == defaultVariable.Id);
    }

    partial void OnSelectedVariableChanged(VariableOption? value)
    {
        var variable = value?.Variable;
        SelectedCodeList = null;
        CodeLists.Clear();

        if (variable == null)
        {
            SelectedVariableDisplay = string.Empty;
            Display = "Select CodeList";
            LoadAllCodeListsAsync().AwaitWithOpt();
            return;
        }

        SelectedVariableDisplay = $"{variable.DatasetName}.{variable.VariableName}";
        Display = $"Create codelist for variable: {SelectedVariableDisplay}";
        CodeListDto.Type = variable.DataType ?? Types[0];
        LoadCodeListsForVariableAsync(variable).AwaitWithOpt();
    }

    partial void OnSelectedCodeListChanged(CodeListOption? value)
    {
        
        var codeListReference = value?.CodeListReference;
        CodeListDto.Code = codeListReference?.CodeListCode;
        CodeListDto.Name = codeListReference?.CodeListName;
        CodeListDto.UniqueId = codeListReference?.CodeListRef?.Split(".").LastOrDefault();
    }

    partial void OnSelectedCommentChanged(ISelectOption? value)
    {
        if (value == null)
        {
            CodeListDto.CommentId = 0;
            CodeListDto.Comment = null;
            CodeListDto.CommentUniqueId = string.Empty;
            return;
        }
        if (value.Content is not Comment comment) return;
        CodeListDto.CommentId =  comment.Id;
        CodeListDto.Comment =  comment;
        CodeListDto.CommentUniqueId =  comment.UniqueId;
    }
    
    private async Task LoadComments()
    {
        if(_currentProjectService.CurrentProject==null) return;
        var comments =  await _commentService.GetAllCommentsAsync();
        List<ISelectOption> res = [];
        foreach (var comment in comments)
        {
            if(string.IsNullOrWhiteSpace(comment.UniqueId) || string.IsNullOrWhiteSpace(comment.Description))
                continue;
            var selectOption = new SelectOption() { Header = comment.UniqueId,Content = comment };
            res.Add(selectOption);
        }
        Comments.Clear();
        Comments.AddRange(res);
    }
    

    private async Task LoadAllCodeListsAsync()
    {
        var codeListReferences = await _codeListService.GetAllCodeListReferencesAsync();
        CodeLists.AddRange(codeListReferences.Select(CreateCodeListOption));
    }

    private async Task LoadCodeListsForVariableAsync(VariableDto variable)
    {
        var codeListRef = await _codeListService.GetCodeListRefByVariableAsync(variable.VariableName?.ToUpperInvariant());
        if (codeListRef != null && !string.IsNullOrWhiteSpace(codeListRef.CodeListRef))
        {
            var codeListReference = await _codeListService.GetCodeListReferenceByOidAsync(codeListRef.CodeListRef);
            if (codeListReference != null)
            {
                var codeListOption = CreateCodeListOption(codeListReference);
                CodeLists.Add(codeListOption);
                SelectedCodeList = codeListOption;
            }

            return;
        }

        var codeListReferences = await _codeListService.GetAllCodeListReferencesAsync();
        CodeLists.AddRange(codeListReferences.Select(CreateCodeListOption));
        CodeListDto.Code = string.Empty;
        CodeListDto.Name = variable.Label;
        CodeListDto.UniqueId = variable.VariableName;
    }

    private static CodeListOption CreateCodeListOption(CodeListReference codeListReference)
    {
        var display = $"{codeListReference.CodeListRef} {codeListReference.CodeListCode} {codeListReference.CodeListName}";
        return new CodeListOption
        {
            Header =  codeListReference.CodeListRef,
            Content = display,
            CodeListReference = codeListReference
        };
    }

    [RelayCommand]
    private async Task Save()
    {
        var projectId = _currentProjectService.CurrentProject?.Id ?? 0;
        if (!IsInEditMode && !string.IsNullOrWhiteSpace(CodeListDto.UniqueId))
        {
            var exists = await _sqlSugar.Queryable<CodeList>()
                .Where(o => o.ProjectId == projectId
                            && o.CdiscDataType == _currentProjectService.CdiscDataType
                            && o.UniqueId == CodeListDto.UniqueId)
                .AnyAsync();
            if (exists)
            {
                _messageService.Error($"CodeList {CodeListDto.UniqueId} already exists in the current project.");
                return;
            }
        }

        CodeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        var codeList = _mapper.Map<CodeList>(CodeListDto);
        codeList.ProjectId = projectId;
        codeList.CdiscDataType = _currentProjectService.CdiscDataType;
        var dialogResult = new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "CodeList", codeList },
                { "Model", IsInEditMode ? CodeListDto : null },
                { "Variable", SelectedVariable?.Variable }
            }
        };
        DialogHost.Close("Root",dialogResult );
    }
    
    [RelayCommand]
    private void Cancel()
    {
        CodeListDto.PropertyChanged -= CodeListDtoOnPropertyChanged;
        DialogHost.Close("Root",new DialogHostResult{Result = DialogButtonResult.Cancel} );
    }
}

public class CodeListOption :SelectOption
{
    public CodeListReference? CodeListReference { get; set; }
}

public class VariableOption : SelectOption
{
    public VariableDto Variable { get; set; } = null!;
}