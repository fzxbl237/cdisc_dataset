using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetAllDocumentDtosAsync(int projectId, CdiscDataType dataType);
    Task<List<DocumentDto>> GetAllDocumentDtosWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<List<Document>> GetAllDocumentsWithoutErorrAsync(int projectId, CdiscDataType dataType);

    Task<List<Document>> GetAllDocumentsAsync(int projectId, CdiscDataType dataType);

    Task<int> DeleteDocumentAsync(Document? document);
    
    Task<int> DeleteDocumentDtoAsync(DocumentDto? document);

    Task<Document> InsertDocumentAsync(Document document);
    
    Task<DocumentDto> InsertDocumentAsync(DocumentDto documentDto);

    Task<int> UpdateDocumentAsync(Document document);

    Task<int> SaveDocumentsAsync(List<DocumentDto> documents);
}
