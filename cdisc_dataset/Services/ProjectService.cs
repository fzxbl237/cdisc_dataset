using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class ProjectService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService) : IProjectService
{
    public async Task<List<Project>> GetAllProjectsAsync()
    {
        return await sqlSugar.Queryable<Project>()
            .ToListAsync();
    }

    public async Task<List<ProjectDto>> GetAllProjectDtosAsync()
    {
        var list = await GetAllProjectsAsync();

        var dtos = mapper.Map<List<ProjectDto>>(list);
        await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(ProjectDto), dto => dto.Id);

        return dtos;
    }

    public async Task<int> DeleteProjectAsync(ProjectDto projectDto)
    {
        return await sqlSugar.Deleteable(mapper.Map<Project>(projectDto)).ExecuteCommandAsync();
    }

    public async Task<int> UpdateProjectAsync(ProjectDto projectDto)
    {
        return await sqlSugar.Updateable(mapper.Map<Project>(projectDto)).ExecuteCommandAsync();
    }

    public async Task<bool> ProjectCodeExistsAsync(string? projectCode)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
        {
            return false;
        }

        return await sqlSugar.Queryable<Project>()
            .AnyAsync(x => x.ProjectCode == projectCode);
    }

    public async Task<ProjectDto> InsertProjectAsync(Project project)
    {
        var entity = await sqlSugar.Insertable(project).ExecuteReturnEntityAsync();
        return mapper.Map<ProjectDto>(entity);
    }

    public async Task<ProjectDto> InsertProjectAsync(ProjectDto projectDto)
    {
        var project = mapper.Map<Project>(projectDto);
        return await InsertProjectAsync(project);
    }

    public async Task<int> SaveProjectsAsync(List<ProjectDto> projects)
    {
        var list = mapper.Map<List<Project>>(projects);
        var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        await issueService.SyncIssuesAsync(projects, nameof(ProjectDto), dto => dto.Id);
        return inserted + updated;
    }
}