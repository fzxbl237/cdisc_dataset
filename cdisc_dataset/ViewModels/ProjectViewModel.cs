using System.Collections.ObjectModel;
using System.Threading.Tasks;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services;
using cdisc_dataset.Messages;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels;

public partial class ProjectViewModel : ObservableObject
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
        Projects.AddRange(await _projectService.GetAllProjectDtosAsync());
    }

    [RelayCommand]
    private async Task Delete(ProjectDto project)
    {
        await _projectService.DeleteProjectAsync(project);
        Projects.Remove(project);
        await WeakReferenceMessenger.Default.Send(new ProjectChangedMessage());
        _messageService.Success("删除成功");
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

        var result = await _dialogHostService.ShowDialog("ProjectDialog", parameters);
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Project"))
            return;

        var updatedProject = result.Parameters.GetValue<ProjectDto>("Project");
        
        await _projectService.UpdateProjectAsync(updatedProject);
        await WeakReferenceMessenger.Default.Send(new ProjectChangedMessage());
        _messageService.Success("更新成功");
    }
    
    [RelayCommand]
    private async Task AddProjectAsync()
    {
        var parameters = new DialogParameters
        {
            { "Title", "New Project" },
            { "IsNotEditMode", true }
        };
        var result = await _dialogHostService.ShowDialog("ProjectDialog", parameters);
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Project"))
            return;

        var project = result.Parameters.GetValue<ProjectDto>("Project");
        if (await _projectService.ProjectCodeExistsAsync(project.ProjectCode))
        {
            _messageService.Error("ProjectCode 已存在，无法新增");
            return;
        }

        await _projectService.InsertProjectAsync(project);
        await WeakReferenceMessenger.Default.Send(new ProjectChangedMessage());
        _messageService.Success("新增成功");
    }
}