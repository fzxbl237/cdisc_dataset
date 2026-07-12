using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Services.Interface;

public interface IProjectService
{
    Task<List<Project>> GetAllProjectsAsync();
    Task<List<ProjectDto>> GetAllProjectDtosAsync();
    Task<int> DeleteProjectAsync(ProjectDto projectDto);
    Task<int> UpdateProjectAsync(ProjectDto projectDto);
    Task<bool> ProjectCodeExistsAsync(string? projectCode);
    Task<ProjectDto> InsertProjectAsync(Project project);
    Task<ProjectDto> InsertProjectAsync(ProjectDto projectDto);
    Task<int> SaveProjectsAsync(List<ProjectDto> projects);
}