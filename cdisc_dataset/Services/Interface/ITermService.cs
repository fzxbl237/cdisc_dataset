using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Services.Interface;

public interface ITermService
{
    Task<List<TermDto>> GetAllTermDtosAsync();
    Task<List<TermDto>> GetAllTermDtosWithoutErorrAsync();
    Task<List<Term>> GetAllTermsWithoutErorrAsync();
    
    Task<List<Term>?> GetTermsByCodeListIdAsync(int? codeListId);
    Task<List<string?>> GetTermCodesByCodeListIdAsync(string? codeListId);

    Task<TermStd?> GetTermStdAsync(string? codeListCode, string? term);
    
    Task<List<TermStd>?> GetTermStdsAsync(string? codeListCode, List<string?> codes);
    
    Task<List<TermStd>?> GetExclusiveTermStdsAsync(string? forCodeListId, string? withCodeListId,string? codeListCode);
    
    Task<int> DeleteTermAsync(TermDto termDto);
    
    Task<int> UpdateTermAsync(TermDto termDto);
    
    Task<TermDto> InsertTermAsync(Term term);
    
    Task<TermDto> InsertTermAsync(TermDto termDto);
    
    Task<int> InsertTermsAsync(List<Term> terms);
    
    Task<int> SaveTermsAsync(List<TermDto> terms);
}