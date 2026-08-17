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

public class CommentService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService, ICurrentProjectService currentProjectService, ILookupStore lookupStore) : ICommentService
{
    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }

    public async Task<List<CommentDto>> GetAllSdtmCommentsAsync()
    {
        var (projectId, _) = GetCurrentProjectContext();
        var comments = await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == CdiscDataType.Sdtm)
            .Includes(o=>o.Document)
            .Select<CommentDto>(o=> new CommentDto(){
                Document = o.Document
            },true).ToListAsync();
        //await RestoreCommentErrorsAsync(comments);
        return comments;
    }

    public async Task<List<CommentDto>> GetAllCommentDtosAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var comments = await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .Includes(o=>o.Document)
            .Select<CommentDto>(o=> new CommentDto(){
                Document = o.Document
            },true).ToListAsync();
        //await RestoreCommentErrorsAsync(comments);
        return comments;
    }

    public async Task<List<CommentDto>> GetAllCommentDtosWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .Select<CommentDto>()
            .ToListAsync();
    }

    public async Task<bool> CommentExistsAsync(string commentUniqueId)
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Comment>().AnyAsync(x => x.ProjectId == projectId && x.CdiscDataType == dataType && x.UniqueId == commentUniqueId);
    }

    public async Task<List<Comment>> GetAllCommentsWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
    }
    
    public async Task<List<Comment>> GetAllCommentsAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Comment>().Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType).ToListAsync();
    }

    public async Task<Comment> UpdateCommentAsync(CommentDto comment)
    {
        var entity = mapper.Map<Comment>(comment);
        var result = await sqlSugar.Updateable(entity).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Comment);
        return result;
    }

    public async Task<int> SaveCommentsAsync(List<CommentDto> comments)
    {
        var list = mapper.Map<List<Comment>>(comments);
        var x = await sqlSugar.Storageable(list).ToStorageAsync();
        var res1 = await x.AsInsertable.ExecuteCommandAsync();
        var res2 = await x.AsUpdateable.ExecuteCommandAsync();

        await issueService.SyncIssuesAsync(comments, nameof(CommentDto), dto => dto.Id);

        await lookupStore.RefreshAsync(LookupKind.Comment);
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
        var codeLists = await sqlSugar.Queryable<CodeList>()
            .Where(x => x.ProjectId == comment.ProjectId && x.CdiscDataType == comment.CdiscDataType && x.CommentId == comment.Id)
            .Select(x => x.UniqueId).ToListAsync();
        var terms = await sqlSugar.Queryable<Term>()
            .Where(x => x.ProjectId == comment.ProjectId && x.CdiscDataType == comment.CdiscDataType && x.CommentId == comment.Id)
            .Select(x => x.Name).ToListAsync();
        var valueLevels = await sqlSugar.Queryable<ValueLevel>()
            .Where(x => x.ProjectId == comment.ProjectId && x.CdiscDataType == comment.CdiscDataType && x.CommentId == comment.Id)
            .Select(x => $"{x.Dataset}.{x.Variable}").ToListAsync();

        if (datasets.Count > 0) dictionary.Add("Datasets", string.Join(", ", datasets));
        if (variables.Count > 0) dictionary.Add("Variables", string.Join(", ", variables));
        if (codeLists.Count > 0) dictionary.Add("CodeLists", string.Join(", ", codeLists));
        if (terms.Count > 0) dictionary.Add("Terms", string.Join(", ", terms));
        if (valueLevels.Count > 0) dictionary.Add("ValueLevels", string.Join(", ", valueLevels));
        return dictionary;
    }

    public async Task<int> DeleteCommentAsync(Comment? comment, bool clearReferences = true)
    {
        var res = 0;
        if (comment == null) return res;
        res = await sqlSugar.Deleteable<Comment>(comment).ExecuteCommandAsync();
        if (clearReferences)
        {
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
            await sqlSugar.Updateable<CodeList>()
                .SetColumns(x => new CodeList { CommentId = 0, CommentUniqueId = string.Empty })
                .Where(x => x.ProjectId == comment.ProjectId && x.CdiscDataType == comment.CdiscDataType && x.CommentId == comment.Id)
                .ExecuteCommandAsync();
            await sqlSugar.Updateable<Term>()
                .SetColumns(x => new Term { CommentId = 0 })
                .Where(x => x.ProjectId == comment.ProjectId && x.CdiscDataType == comment.CdiscDataType && x.CommentId == comment.Id)
                .ExecuteCommandAsync();
            await sqlSugar.Updateable<ValueLevel>()
                .SetColumns(x => new ValueLevel { CommentId = 0, CommentUniqueId = string.Empty })
                .Where(x => x.ProjectId == comment.ProjectId && x.CdiscDataType == comment.CdiscDataType && x.CommentId == comment.Id)
                .ExecuteCommandAsync();
        }
        await lookupStore.RefreshAsync(LookupKind.Comment);
        return res;
    }

    public async Task<Comment> InsertCommentAsync(Comment comment)
    {
        var entity = await sqlSugar.InsertNav(comment).Include(o=>o.Document).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Comment);
        return entity;
    }

    public async Task<CommentDto> InsertCommentAsync(CommentDto commentDto)
    {
        var comment = mapper.Map<Comment>(commentDto);
        var entity = await InsertCommentAsync(comment);
        return mapper.Map<CommentDto>(entity);
    }

    public async Task<Comment> UpdateCommentAsync(Comment comment)
    {
        var result = await sqlSugar.Updateable(comment).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Comment);
        return result;
    }


    public List<CommentDto> GetAllSdtmComments()
    {
        var (projectId, _) = GetCurrentProjectContext();
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
