using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class CommentService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService) : ICommentService
{
    public async Task<List<CommentDto>> GetAllSdtmCommentsAsync(int projectId)
    {
        var comments = await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == CdiscDataType.Sdtm).Select<CommentDto>().ToListAsync();
        await RestoreCommentErrorsAsync(comments);
        return comments;
    }

    public async Task<List<CommentDto>> GetAllCommentDtosAsync(int projectId, CdiscDataType dataType)
    {
        var comments = await sqlSugar.Queryable<Comment>().Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType).Select<CommentDto>().ToListAsync();
        await RestoreCommentErrorsAsync(comments);
        return comments;
    }

    public async Task<List<CommentDto>> GetAllCommentDtosWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        return await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .Select<CommentDto>()
            .ToListAsync();
    }

    public async Task<bool> CommentExistsAsync(int projectId, CdiscDataType dataType, string commentUniqueId)
    {
        return await sqlSugar.Queryable<Comment>().AnyAsync(x => x.ProjectId == projectId && x.CdiscDataType == dataType && x.UniqueId == commentUniqueId);
    }

    public async Task<List<Comment>> GetAllCommentsWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        return await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
    }
    
    public async Task<List<Comment>> GetAllCommentsAsync(int projectId, CdiscDataType dataType)
    {
        return await sqlSugar.Queryable<Comment>().Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType).ToListAsync();
    }

    public async Task<Comment> UpdateCommentAsync(CommentDto comment)
    {
        var entity = mapper.Map<Comment>(comment);
        return await sqlSugar.Updateable(entity).ExecuteReturnEntityAsync();
    }

    public async Task<int> SaveCommentsAsync(List<CommentDto> comments)
    {
        var list = mapper.Map<List<Comment>>(comments);
        var x = await sqlSugar.Storageable(list).ToStorageAsync();
        var res1 = await x.AsInsertable.ExecuteCommandAsync();
        var res2 = await x.AsUpdateable.ExecuteCommandAsync();

        await issueService.SyncIssuesAsync(comments, nameof(CommentDto), dto => dto.Id);

        return res1 + res2;
    }

    // TODO: Value Level
    public async Task<Dictionary<string, string>> ConfirmCommentRefenceAsync(Comment? comment)
    {
        var dictionary = new Dictionary<string, string>();
        if (comment == null) return dictionary;
        var datasets = await sqlSugar.Queryable<Dataset>()
            .Where(x => x.ProjectId == comment.ProjectId 
                        && x.CdiscDataType == comment.CdiscDataType
                        && x.CommentId == comment.Id)
            .Select(o => o.Name).ToListAsync();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == comment.ProjectId 
                        && x.CdiscDataType == comment.CdiscDataType
                        && x.CommentId == comment.Id)
            .Select(o => $"{o.DatasetName}.{o.VariableName}").ToListAsync();
        if (variables.Count > 0)
        {
            dictionary.Add("Variables", string.Join(", ", variables));
        }

        if (datasets.Count > 0)
        {
            dictionary.Add("Datasets", string.Join(", ", datasets));
        }
        return dictionary;
    }

    public async Task<int> DeleteCommentAsync(Comment? comment)
    {
        var res = 0;
        if (comment == null) return res;
        res = await sqlSugar.Deleteable<Comment>(comment).ExecuteCommandAsync();
        var datasets = await sqlSugar.Queryable<Dataset>()
            .Where(x => x.ProjectId == comment.ProjectId 
                        && x.CdiscDataType == comment.CdiscDataType
                        && x.CommentId == comment.Id)
            .ToListAsync();
        foreach (var dataset in datasets)
        {
            dataset.CommentId = 0;
            dataset.CommentUniqueId = string.Empty;
        }
        await sqlSugar.Updateable(datasets).ExecuteCommandAsync();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == comment.ProjectId 
                        && x.CdiscDataType == comment.CdiscDataType
                        && x.CommentId == comment.Id)
            .ToListAsync();
        foreach (var variable in variables)
        {
            variable.CommentId = 0;
            variable.CommentUniqueId = string.Empty;
        }
        await sqlSugar.Updateable(variables).ExecuteCommandAsync();
        return res;
    }

    public async Task<Comment> InsertCommentAsync(Comment comment)
    {
        return await sqlSugar.Insertable(comment).ExecuteReturnEntityAsync();
    }

    public async Task<CommentDto> InsertCommentAsync(CommentDto commentDto)
    {
        var comment = mapper.Map<Comment>(commentDto);
        var entity = await InsertCommentAsync(comment);
        return mapper.Map<CommentDto>(entity);
    }

    public async Task<Comment> UpdateCommentAsync(Comment comment)
    {
        return await sqlSugar.Updateable(comment).ExecuteReturnEntityAsync();
    }


    public List<CommentDto> GetAllSdtmComments(int projectId)
    {
        var comments = sqlSugar.Queryable<Comment>().Where(x => x.ProjectId == projectId && x.CdiscDataType == CdiscDataType.Sdtm).Select<CommentDto>().ToList();
        RestoreCommentErrorsAsync(comments).GetAwaiter().GetResult();
        return comments;
    }

    private async Task RestoreCommentErrorsAsync(IEnumerable<CommentDto> comments)
    {
        foreach (var comment in comments)
        {
            if (string.IsNullOrWhiteSpace(comment.UniqueId))
            {
                continue;
            }

            await issueService.RestoreErrorsAsync(comment, nameof(CommentDto), comment.Id,comment.ProjectId, comment.CdiscDataType);
        }
    }
}
