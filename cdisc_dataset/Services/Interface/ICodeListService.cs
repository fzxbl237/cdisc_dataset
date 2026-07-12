using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface ICodeListService
{
    Task<List<CodeListDto>> GetAllCodeListDtosAsync(int projectId,CdiscDataType dataType);
    Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<List<CodeList>> GetAllCodeListsWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<List<CodeList>> GetAllCodeListsAsync(int projectId,CdiscDataType dataType);
    
    Task<CodeListStd> GetCodeListStdAsync(string version,string codeListCode);
    
    Task<bool> DeleteCodeListAsync(CodeListDto codeListDto);
    
    Task<int> UpdateCodeListAsync(CodeListDto codeListDto);
    
    Task<CodeListDto> InsertCodeListAsync(CodeList codeList);
    
    Task<CodeListDto> InsertCodeListAsync(CodeListDto codeListDto);

    Task<List<string?>> GetTerminologiesAsync();
    
    Task<int> SaveCodeListsAsync(List<CodeListDto> codeLists);
    
    Task<bool> VariableHasCodeListAsync(string? variableName);
}