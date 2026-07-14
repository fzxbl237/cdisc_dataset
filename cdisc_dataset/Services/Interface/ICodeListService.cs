using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;

namespace cdisc_dataset.Services.Interface;

public interface ICodeListService
{
    Task<List<CodeListDto>> GetAllCodeListDtosAsync();
    Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync();
    Task<List<CodeList>> GetAllCodeListsWithoutErorrAsync();
    Task<List<CodeList>> GetAllCodeListsAsync();
    
    Task<CodeListStd> GetCodeListStdAsync(string version,string codeListCode);
    
    Task<bool> DeleteCodeListAsync(CodeListDto codeListDto);
    
    Task<int> UpdateCodeListAsync(CodeListDto codeListDto);
    
    Task<CodeListDto> InsertCodeListAsync(CodeList codeList);
    
    Task<CodeListDto> InsertCodeListAsync(CodeListDto codeListDto);

    Task<List<string?>> GetTerminologiesAsync();
    
    Task<int> SaveCodeListsAsync(List<CodeListDto> codeLists);
    
    // Task<bool> VariableHasCodeListAsync(string? variableName);

    Task<VariableCodeList?> GetCodeListRefByVariableAsync(string? variableName);
    
    Task<CodeListTerm?> GetCodeListTermAsync(string? codeListOid,string? term);
    
    Task<List<CodeListTerm>> GetCodeListTermsAsync(string? codeListOid);
    
    Task<CodeListReference?> GetCodeListReferenceByOidAsync(string? codeListOid);
}