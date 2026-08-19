using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;

namespace PatChes.Services.Interface;

public interface IDictionaryService
{
    Task<List<Dictionary>> GetAllDictionariesAsync();
    Task<List<Dictionary>> GetAllDictionariesWithoutErorrAsync();
    Task<List<DictionaryDto>> GetAllDictionaryDtosAsync();
    Task<List<DictionaryDto>> GetAllDictionaryDtosWithoutErorrAsync();
    Task<List<string?>> GetDictionaryVersionsByDictionaryNameAsync(string dictionaryName);
    Task<List<string>> GetAllDictionaryNamesAsync();
    Task<bool> DictionaryExistsAsync(string dictionaryUniqueId);
    Task<Dictionary?> GetDictionaryByIdAsync(int id);
    Task<Dictionary> InsertDictionaryAsync(DictionaryDto dictionary);
    Task<Dictionary> UpdateDictionaryAsync(DictionaryDto dictionary);
    Task<Dictionary<string, string>> ConfirmDictionaryReferenceAsync(DictionaryDto dictionary);
    Task<int> DeleteDictionaryAsync(DictionaryDto dictionary, bool clearReferences = true);
    Task<int> SaveDictionariesAsync(List<DictionaryDto> dictionaries);
}
