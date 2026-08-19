using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Models.Settings;

namespace PatChes.Services.Interface;

public interface ICodeListService
{
    Task<List<CodeListDto>> GetAllCodeListDtosAsync();
    Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync();
    Task<List<CodeList>> GetAllCodeListsWithoutErorrAsync();
    Task<List<CodeList>> GetAllCodeListsAsync();
    
    Task<CodeListStd> GetCodeListStdAsync(string version,string codeListCode);
    
    Task<Dictionary<string, string>> ConfirmCodeListReferenceAsync(CodeListDto codeListDto);

    Task<bool> DeleteCodeListAsync(CodeListDto codeListDto, bool clearReferences = true);
    
    Task<int> UpdateCodeListAsync(CodeListDto codeListDto);

    Task<int> UpdateCodeListWithTermsAsync(CodeListDto codeListDto);
    
    Task<CodeListDto> InsertCodeListAsync(CodeList codeList);
    
    Task<CodeListDto> InsertCodeListAsync(CodeListDto codeListDto);

    Task MergeCodeListsAsync(CodeListDto mergedCodeList, List<int> sourceCodeListIds);

    Task<List<string?>> GetTerminologiesAsync();
    
    Task<int> SaveCodeListsAsync(List<CodeListDto> codeLists);
    
    // Task<bool> VariableHasCodeListAsync(string? variableName);

    Task<VariableCodeList?> GetCodeListRefByVariableAsync(string? variableName);
    
    Task<CodeListTerm?> GetCodeListTermAsync(string? codeListOid,string? term);
    
    Task<List<CodeListTerm>> GetCodeListTermsAsync(string? codeListOid);
    
    Task<CodeListReference?> GetCodeListReferenceByOidAsync(string? codeListOid);

    Task<List<CodeListReference>> GetAllCodeListReferencesAsync();
}