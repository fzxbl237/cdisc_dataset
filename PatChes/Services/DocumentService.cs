using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Models.Settings;
using PatChes.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace PatChes.Services;

public class DocumentService(ISqlSugarClient sqlSugar, IMapper mapper, ICurrentProjectService currentProjectService, ILookupStore lookupStore) : IDocumentService
{
    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }

    public async Task<List<DocumentDto>> GetAllDocumentDtosAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Document>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .Select<DocumentDto>()
            .ToListAsync();
    }

    public async Task<List<DocumentDto>> GetAllDocumentDtosWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Document>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .Select<DocumentDto>()
            .ToListAsync();
    }

    public async Task<List<Document>> GetAllDocumentsWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Document>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
    }

    public async Task<List<Document>> GetAllDocumentsAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Document>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .ToListAsync();
    }

    public async Task<Dictionary<string, string>> ConfirmDocumentReferenceAsync(DocumentDto document)
    {
        var references = new Dictionary<string, string>();
        var comments = await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == document.ProjectId && x.CdiscDataType == document.CdiscDataType && x.DocumentId == document.Id)
            .Select(x => x.UniqueId)
            .ToListAsync();
        var methods = await sqlSugar.Queryable<Method>()
            .Where(x => x.ProjectId == document.ProjectId && x.CdiscDataType == document.CdiscDataType && x.DocumentId == document.Id)
            .Select(x => x.UniqueId)
            .ToListAsync();

        if (comments.Count > 0) references.Add("Comments", string.Join(", ", comments));
        if (methods.Count > 0) references.Add("Methods", string.Join(", ", methods));
        return references;
    }

    public async Task<int> DeleteDocumentAsync(Document? document, bool clearReferences = true)
    {
        if (document == null)
            return 0;

        if (clearReferences)
            await ClearDocumentReferencesAsync(document.Id, document.ProjectId, document.CdiscDataType);

        var result = await sqlSugar.Deleteable(document).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return result;
    }

    public async Task<int> DeleteDocumentDtoAsync(DocumentDto? document, bool clearReferences = true)
    {
        if (document == null)
            return 0;

        if (clearReferences)
            await ClearDocumentReferencesAsync(document.Id, document.ProjectId, document.CdiscDataType);

        var result = await sqlSugar.Deleteable(mapper.Map<Document>(document)).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return result;
    }

    private async Task ClearDocumentReferencesAsync(int documentId, int projectId, CdiscDataType dataType)
    {
        await sqlSugar.Updateable<Comment>()
            .SetColumns(x => new Comment { DocumentId = 0, DocumentUniqueId = string.Empty, Pages = string.Empty })
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && x.DocumentId == documentId)
            .ExecuteCommandAsync();
        await sqlSugar.Updateable<Method>()
            .SetColumns(x => new Method { DocumentId = 0, DocumentUniqueId = string.Empty, Pages = string.Empty })
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && x.DocumentId == documentId)
            .ExecuteCommandAsync();
    }

    public async Task<Document> InsertDocumentAsync(Document document)
    {
        var entity = await sqlSugar.Insertable(document).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return entity;
    }

    public async Task<DocumentDto> InsertDocumentAsync(DocumentDto documentDto)
    {
        var document = mapper.Map<Document>(documentDto);
        var entity = await InsertDocumentAsync(document);
        return mapper.Map<DocumentDto>(entity);
    }

    public async Task<int> UpdateDocumentAsync(Document document)
    {
        var result = await sqlSugar.Updateable(document).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return result;
    }

    public async Task<int> UpdateDocumentAsync(DocumentDto document)
    {
        var doc = mapper.Map<Document>(document);
        var result = await sqlSugar.Updateable(doc).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return result;
    }

    public async Task<List<TemplateDocument>> GetAvailableSettingDocumentsAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        if (projectId == 0)
            return [];

        var existingUniqueIds = await sqlSugar.Queryable<Document>()
            .Where(document => document.ProjectId == projectId && document.CdiscDataType == dataType)
            .Select(document => document.UniqueId)
            .ToListAsync();

        return await sqlSugar.AsTenant().QueryableWithAttr<TemplateDocument>()
            .Where(document => document.CdiscDataType == dataType &&
                               !string.IsNullOrWhiteSpace(document.UniqueId) &&
                               !existingUniqueIds.Contains(document.UniqueId))
            .ToListAsync();
    }

    public async Task<int> ImportSettingDocumentsAsync(IReadOnlyList<int> templateDocumentIds)
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var templateIds = templateDocumentIds.Distinct().ToList();
        if (projectId == 0 || templateIds.Count == 0)
            return 0;

        var existingUniqueIds = await sqlSugar.Queryable<Document>()
            .Where(document => document.ProjectId == projectId && document.CdiscDataType == dataType)
            .Select(document => document.UniqueId)
            .ToListAsync();

        var templates = await sqlSugar.AsTenant().QueryableWithAttr<TemplateDocument>()
            .Where(document => templateIds.Contains(document.Id) &&
                               document.CdiscDataType == dataType &&
                               !string.IsNullOrWhiteSpace(document.UniqueId) &&
                               !existingUniqueIds.Contains(document.UniqueId))
            .ToListAsync();

        if (templates.Count == 0)
            return 0;

        var documents = templates.Select(template => new Document
        {
            UniqueId = template.UniqueId,
            Title = template.Title,
            Href = template.Href,
            ProjectId = projectId,
            CdiscDataType = dataType
        }).ToList();

        var result = await sqlSugar.Insertable(documents).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return result;
    }

    public async Task<int> SaveDocumentsAsync(List<DocumentDto> documents)
    {
        var list = mapper.Map<List<Document>>(documents);
        var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Document);
        return inserted + updated;
    }
}
