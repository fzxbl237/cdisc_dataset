using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace PatChes.Services;

public class DictionaryService(ISqlSugarClient sqlSugar, ICurrentProjectService currentProjectService,IIssueService issueService, IMapper mapper, ILookupStore lookupStore) : IDictionaryService
{
    private CdiscDataType CurrentDataType => currentProjectService.CdiscDataType;
    private int CurrentProjectId => currentProjectService.CurrentProject?.Id ?? 0;
    
    public async Task<List<Dictionary>> GetAllDictionariesAsync()
    {
        return await sqlSugar.Queryable<Dictionary>()
            .Where(x => x.ProjectId == CurrentProjectId && x.CdiscDataType == CurrentDataType)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<List<Dictionary>> GetAllDictionariesWithoutErorrAsync()
    {
        return await sqlSugar.Queryable<Dictionary>()
            .Where(x => x.ProjectId == CurrentProjectId && x.CdiscDataType == CurrentDataType && !x.HasErrors)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<List<DictionaryDto>> GetAllDictionaryDtosAsync()
    {
        var dictionaryDtos = await sqlSugar.Queryable<Dictionary>()
            .Where(x => x.ProjectId == CurrentProjectId && x.CdiscDataType == CurrentDataType)
            .OrderBy(x => x.Id)
            .Select<DictionaryDto>()
            .ToListAsync();
        await issueService.RestoreIssuesAsync(dictionaryDtos, nameof(DictionaryDto), dto => dto.Id);
        return dictionaryDtos;
    }

    public async Task<List<DictionaryDto>> GetAllDictionaryDtosWithoutErorrAsync()
    {
        return await sqlSugar.Queryable<Dictionary>()
            .Where(x => x.ProjectId == CurrentProjectId && x.CdiscDataType == CurrentDataType && !x.HasErrors)
            .OrderBy(x => x.Id)
            .Select<DictionaryDto>()
            .ToListAsync();
    }

    public async Task<List<string?>> GetDictionaryVersionsByDictionaryNameAsync(string dictionaryName)
    {
        if (string.IsNullOrWhiteSpace(dictionaryName))
            return [];

        return await sqlSugar.Queryable<DictionaryVersion>()
            .Where(x => x.DictionaryName == dictionaryName)
            .OrderBy(x => x.Version)
            .Select(x => x.Version)
            .ToListAsync();
    }

    public async Task<List<string>> GetAllDictionaryNamesAsync()
    {
        return await sqlSugar.Queryable<DictionaryVersion>()
            .Where(x => !string.IsNullOrWhiteSpace(x.DictionaryName))
            .Select(x=>x.DictionaryName!)
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> DictionaryExistsAsync(string dictionaryUniqueId)
    {
        return await sqlSugar.Queryable<Dictionary>()
            .AnyAsync(x => x.ProjectId == CurrentProjectId && x.CdiscDataType == CurrentDataType && x.UniqueId == dictionaryUniqueId);
    }

    public async Task<Dictionary?> GetDictionaryByIdAsync(int id)
    {
        return await sqlSugar.Queryable<Dictionary>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<Dictionary> InsertDictionaryAsync(DictionaryDto dictionary)
    {
        var entity = mapper.Map<Dictionary>(dictionary);
        var result = await sqlSugar.Insertable(entity).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Dictionary);
        return result;
    }

    public async Task<Dictionary> UpdateDictionaryAsync(DictionaryDto dictionary)
    {
        var entity = mapper.Map<Dictionary>(dictionary);
        var result = await sqlSugar.Updateable(entity).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Dictionary);
        return result;
    }

    public async Task<Dictionary<string, string>> ConfirmDictionaryReferenceAsync(DictionaryDto dictionary)
    {
        var references = new Dictionary<string, string>();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == dictionary.ProjectId && x.CdiscDataType == dictionary.CdiscDataType && x.DictionaryId == dictionary.Id)
            .Select(x => $"{x.DatasetName}.{x.VariableName}")
            .ToListAsync();

        if (variables.Count > 0) references.Add("Variables", string.Join(", ", variables));
        return references;
    }

    public async Task<int> DeleteDictionaryAsync(DictionaryDto dictionary, bool clearReferences = true)
    {
        if (clearReferences)
        {
            await sqlSugar.Updateable<Variable>()
                .SetColumns(x => new Variable
                {
                    DictionaryId = 0,
                    DictionaryUniqueId = string.Empty,
                    CodeListUniqueId = string.Empty
                })
                .Where(x => x.ProjectId == dictionary.ProjectId && x.CdiscDataType == dictionary.CdiscDataType && x.DictionaryId == dictionary.Id)
                .ExecuteCommandAsync();
        }

        var entity = mapper.Map<Dictionary>(dictionary);
        var result = await sqlSugar.Deleteable(entity).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Dictionary);
        return result;
    }

    public async Task<int> SaveDictionariesAsync(List<DictionaryDto> dictionaries)
    {
        var list = mapper.Map<List<Dictionary>>(dictionaries);
        var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        await issueService.SyncIssuesAsync(dictionaries, nameof(DictionaryDto), dto => dto.Id);
        await lookupStore.RefreshAsync(LookupKind.Dictionary);
        return inserted + updated;
    }
}
