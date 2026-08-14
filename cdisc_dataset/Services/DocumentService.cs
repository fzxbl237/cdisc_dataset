using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class DocumentService(ISqlSugarClient sqlSugar, IMapper mapper, ICurrentProjectService currentProjectService) : IDocumentService
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

    public async Task<int> DeleteDocumentAsync(Document? document)
    {
        if (document == null)
            return 0;

        return await sqlSugar.Deleteable(document).ExecuteCommandAsync();
    }

    public async Task<int> DeleteDocumentDtoAsync(DocumentDto? document)
    {
        if (document == null)
            return 0;
        return await sqlSugar.Deleteable(mapper.Map<Document>(document)).ExecuteCommandAsync();
    }

    public async Task<Document> InsertDocumentAsync(Document document)
    {
        return await sqlSugar.Insertable(document).ExecuteReturnEntityAsync();
    }

    public async Task<DocumentDto> InsertDocumentAsync(DocumentDto documentDto)
    {
        var document = mapper.Map<Document>(documentDto);
        var entity = await InsertDocumentAsync(document);
        return mapper.Map<DocumentDto>(entity);
    }

    public async Task<int> UpdateDocumentAsync(Document document)
    {
        return await sqlSugar.Updateable(document).ExecuteCommandAsync();
    }

    public async Task<int> UpdateDocumentAsync(DocumentDto document)
    {
        var doc = mapper.Map<Document>(document);
        return await sqlSugar.Updateable(doc).ExecuteCommandAsync();
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

        return await sqlSugar.Insertable(documents).ExecuteCommandAsync();
    }

    public async Task<int> SaveDocumentsAsync(List<DocumentDto> documents)
    {
        var list = mapper.Map<List<Document>>(documents);
        var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        return inserted + updated;
    }
}
