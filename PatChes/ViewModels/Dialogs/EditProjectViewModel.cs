using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using AtomUI.Controls;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Services;
using PatChes.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

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

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
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
        DialogHost.Close(DialogHostName, new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "Project", Project }
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName, new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}