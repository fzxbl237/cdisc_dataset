using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PatChes.Models.Dto;
using PatChes.Services;
using PatChes.Messages;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels;

public partial class ProjectViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IMessageService _messageService;

    public ObservableCollection<ProjectDto> Projects { get; set; }

    public ProjectViewModel(IProjectService projectService, IDialogHostService dialogHostService,IMessageService messageService)
    {
        _projectService = projectService;
        _dialogHostService = dialogHostService;
        _messageService = messageService;
        Projects = new ObservableCollection<ProjectDto>();
        LoadProjects();
    }

    private async void LoadProjects()
    {
        Projects.Clear();
        foreach (var project in await _projectService.GetAllProjectDtosAsync())
        {
            Projects.Add(project);
        }
    }

    [RelayCommand]
    private async Task Delete(ProjectDto project)
    {
        await _projectService.DeleteProjectAsync(project);
        Projects.Remove(project);
        await WeakReferenceMessenger.Default.Send(new ProjectChangedMessage());
        _messageService.Success("ɾ���ɹ�");
    }

    [RelayCommand]
    private async Task Modify(ProjectDto project)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Modify Project" },
            { "Project", project },
            { "IsNotEditMode", false }
        };

        var result = await _dialogHostService.ShowDialogAsync("ProjectDialog", parameters);
        if (result.Result != DialogButtonResult.Yes || !result.Parameters.ContainsKey("Project"))
            return;

        var updatedProject = result.Parameters.GetValue<ProjectDto>("Project");
        
        await _projectService.UpdateProjectAsync(updatedProject);
        await WeakReferenceMessenger.Default.Send(new ProjectChangedMessage());
        _messageService.Success("���³ɹ�");
    }
    
    [RelayCommand]
    private async Task AddProjectAsync()
    {
        var parameters = new DialogParameters
        {
            { "Title", "New Project" },
            { "IsNotEditMode", true }
        };
        var result = await _dialogHostService.ShowDialogAsync("ProjectDialog", parameters);
        if (result.Result != DialogButtonResult.Yes || !result.Parameters.ContainsKey("Project"))
            return;

        var project = result.Parameters.GetValue<ProjectDto>("Project");
        if (await _projectService.ProjectCodeExistsAsync(project.ProjectCode))
        {
            _messageService.Error("ProjectCode �Ѵ��ڣ��޷�����");
            return;
        }

        await _projectService.InsertProjectAsync(project);
        await WeakReferenceMessenger.Default.Send(new ProjectChangedMessage());
        _messageService.Success("�����ɹ�");
    }
}