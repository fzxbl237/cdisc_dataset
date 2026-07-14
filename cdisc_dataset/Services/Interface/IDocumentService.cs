using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Services.Interface;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetAllDocumentDtosAsync();
    Task<List<DocumentDto>> GetAllDocumentDtosWithoutErorrAsync();
    Task<List<Document>> GetAllDocumentsWithoutErorrAsync();

    Task<List<Document>> GetAllDocumentsAsync();

    Task<int> DeleteDocumentAsync(Document? document);
    
    Task<int> DeleteDocumentDtoAsync(DocumentDto? document);

    Task<Document> InsertDocumentAsync(Document document);
    
    Task<DocumentDto> InsertDocumentAsync(DocumentDto documentDto);

    Task<int> UpdateDocumentAsync(Document document);

    Task<int> SaveDocumentsAsync(List<DocumentDto> documents);
}
