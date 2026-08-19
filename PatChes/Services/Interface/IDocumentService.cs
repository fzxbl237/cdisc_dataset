using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Settings;

namespace PatChes.Services.Interface;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetAllDocumentDtosAsync();
    Task<List<DocumentDto>> GetAllDocumentDtosWithoutErorrAsync();
    Task<List<Document>> GetAllDocumentsWithoutErorrAsync();

    Task<List<Document>> GetAllDocumentsAsync();

    Task<Dictionary<string, string>> ConfirmDocumentReferenceAsync(DocumentDto document);

    Task<int> DeleteDocumentAsync(Document? document, bool clearReferences = true);
    
    Task<int> DeleteDocumentDtoAsync(DocumentDto? document, bool clearReferences = true);

    Task<Document> InsertDocumentAsync(Document document);
    
    Task<DocumentDto> InsertDocumentAsync(DocumentDto documentDto);

    Task<int> UpdateDocumentAsync(Document document);
    
    Task<int> UpdateDocumentAsync(DocumentDto document);

    Task<List<TemplateDocument>> GetAvailableSettingDocumentsAsync();

    Task<int> ImportSettingDocumentsAsync(IReadOnlyList<int> templateDocumentIds);

    Task<int> SaveDocumentsAsync(List<DocumentDto> documents);
}
