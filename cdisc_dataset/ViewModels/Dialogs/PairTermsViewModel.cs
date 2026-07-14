using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class PairTermsViewModel:ObservableObject,IDialogHostAware
{
    private readonly ICodeListService _codeListService;
    private readonly ITermService _termService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly PairCodeListValidator _pairCodeListValidator;

    public string? DialogHostName { get; set; }
    
    [ObservableProperty]
    private AvaloniaList<ISelectOption> _codeListOptions = [];
    
    [ObservableProperty]
    private ISelectOption? _forCodeListOption;
    
    [ObservableProperty]
    private ISelectOption? _withCodeListOption;
    
    [ObservableProperty] private int _currentProjectId;
    [ObservableProperty] private CdiscDataType _cdiscDataType;
    
    [ObservableProperty] 
    private IList<IFormValidator>  _validators = [new FormNotNullValidator(){Message = "Please select with codeList"}];
    
    [ObservableProperty] 
    private DefaultFilterValueSelector _valueFilterPropertySelector = data =>
    {
        if (data is ISelectOption option)
        {
            return option.Content;
        }
        return null;
    };

    public PairTermsViewModel(ICodeListService  codeListService,
        ITermService termService,
        ICurrentProjectService currentProjectService,
        PairCodeListValidator pairCodeListValidator)
    {
        _codeListService = codeListService;
        _termService = termService;
        _currentProjectService = currentProjectService;
        _pairCodeListValidator = pairCodeListValidator;
        Validators.Add(_pairCodeListValidator);
    }
    
    public void OnDialogOpened(IDialogParameters parameters)
    {
        parameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        CdiscDataType = cdiscDataType;
        if (_currentProjectService.CurrentProject != null) 
            CurrentProjectId = _currentProjectService.CurrentProject.Id;
        LoadCodeLists().Await();
    }

    partial void OnForCodeListOptionChanged(ISelectOption? value)
    {
        if (value?.Content is string content)
            _pairCodeListValidator.ForCodeList = content;
    }


    private async Task LoadCodeLists()
    {
        
        var codeLists = await _codeListService.GetAllCodeListDtosAsync();
        List<ISelectOption> res = [];
        foreach (var dto in codeLists)
        {
            if(string.IsNullOrWhiteSpace(dto.UniqueId) || string.IsNullOrWhiteSpace(dto.Name))
                continue;
            var selectOption = new CodeListSelectOption() { 
                Header = dto.UniqueId,
                Content = $"{dto.UniqueId} {dto.Name}",
                CodeListUniqueId = dto.UniqueId,
                Name = dto.Name,
                CodeList = dto
            };
            res.Add(selectOption);
        }
        CodeListOptions.Clear();
        CodeListOptions.AddRange(res);
    }
    
    
    [RelayCommand]
    private async Task Confirm()
    {
        if (ForCodeListOption is CodeListSelectOption forOption
            && WithCodeListOption is CodeListSelectOption withOption)
        {
            List<TermStd>? termStds = await _termService.GetExclusiveTermStdsAsync(forOption.CodeListUniqueId,
                withOption.CodeListUniqueId,forOption.CodeList?.Code);
            List<Term> res = [];
            if (termStds is { Count: > 0 })
            {
                foreach (var termStd in termStds)
                {
                    var term = new Term
                    {
                        Code = termStd.Code,
                        DecodedValue = termStd.Synonyms?.Split(";").FirstOrDefault(),
                        ProjectId = CurrentProjectId,
                        CdiscDataType = CdiscDataType,
                        Name = termStd.Name,
                        CodeListId = forOption.CodeList?.Id??0,
                        CodeListUniqueId = forOption.CodeListUniqueId,
                        Order = 1
                    };
                    res.Add(term);
                }
            }

            await _termService.InsertTermsAsync(res);
        }
        
        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            // Parameters = new DialogParameters{{"CodeList",codeList}}
        };
        DialogHost.Close("Root",dialogResult );
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root",new DialogResult{Result = ButtonResult.Cancel} );
    }
}