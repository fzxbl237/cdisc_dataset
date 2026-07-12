using System;
using System.Collections.Generic;
using AtomUI.Controls;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services;
using cdisc_dataset.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class EditProjectViewModel : ObservableObject, IDialogHostAware
{
    private readonly IValidator<ProjectDto> _validator;
    private readonly FormProjectValidator _formProjectValidator;

    [ObservableProperty]
    private ProjectDto _project = new();
    
    
    [ObservableProperty] 
    private IList<IFormValidator>  _validators = [];

    public EditProjectViewModel(IValidator<ProjectDto> validator,FormProjectValidator formProjectValidator)
    {
        _validator = validator;
        _formProjectValidator = formProjectValidator;
    }

    [ObservableProperty]
    private bool _isNotEditMode = true;

    [ObservableProperty]
    private string? _title;

    public SdtmIgVersion[] SdtmIgVersions => Enum.GetValues<SdtmIgVersion>();
    public AdamIgVersion[] AdamIgVersions => Enum.GetValues<AdamIgVersion>();
    public Language[] Languages => Enum.GetValues<Language>();

    public string DialogHostName { get; set; } = "Root";

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");

        if (parameters.ContainsKey("Project"))
        {
            Project = parameters.GetValue<ProjectDto>("Project");
        }

        if (parameters.ContainsKey("IsNotEditMode"))
            IsNotEditMode = parameters.GetValue<bool>("IsNotEditMode");
        
        _formProjectValidator.Validator = _validator;
        _formProjectValidator.ProjectDto = Project;
        
        Validators.Add(_formProjectValidator);
    }

    [RelayCommand]
    private void Confirm()
    {
        DialogHost.Close(DialogHostName, new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "Project", Project }
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName, new DialogResult { Result = ButtonResult.Cancel });
    }
}