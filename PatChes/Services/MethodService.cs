using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Models.Settings;
using PatChes.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace PatChes.Services;

public class MethodService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService, ICurrentProjectService currentProjectService, ILookupStore lookupStore, IVariableService variableService) : IMethodService
{
    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }

    public async Task<List<MethodDto>> GetAllMethodDtosAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var dtos = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType)
            .Select(o => new MethodDto
            {
                Document = o.Document
            }, true)
            .ToListAsync();
        await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(MethodDto), dto => dto.Id);

        return dtos;
    }

    public async Task<List<MethodDto>> GetAllMethodDtosWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType && !x.HasErrors)
            .Select(o => new MethodDto
            {
                Document = o.Document
            }, true)
            .ToListAsync();
    }

    public async Task<List<Method>> GetAllMethodsWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType && !x.HasErrors)
            .ToListAsync();

        return list;
    }

    public async Task<Dictionary<string, string>> ConfirmMethodReferenceAsync(MethodDto methodDto)
    {
        var references = new Dictionary<string, string>();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == methodDto.ProjectId && x.CdiscDataType == methodDto.CdiscDataType && x.MethodId == methodDto.Id)
            .Select(x => $"{x.DatasetName}.{x.VariableName}")
            .ToListAsync();
        var valueLevels = await sqlSugar.Queryable<ValueLevel>()
            .Where(x => x.ProjectId == methodDto.ProjectId && x.CdiscDataType == methodDto.CdiscDataType && x.MethodId == methodDto.Id)
            .Select(x => $"{x.Dataset}.{x.Variable}")
            .ToListAsync();

        if (variables.Count > 0) references.Add("Variables", string.Join(", ", variables));
        if (valueLevels.Count > 0) references.Add("ValueLevels", string.Join(", ", valueLevels));
        return references;
    }

    public async Task<int> DeleteMethodAsync(MethodDto methodDto, bool clearReferences = true)
    {
        if (clearReferences)
        {
            await sqlSugar.Updateable<Variable>()
                .SetColumns(x => new Variable { MethodId = 0, MethodUniqueId = string.Empty })
                .Where(x => x.ProjectId == methodDto.ProjectId && x.CdiscDataType == methodDto.CdiscDataType && x.MethodId == methodDto.Id)
                .ExecuteCommandAsync();
            await sqlSugar.Updateable<ValueLevel>()
                .SetColumns(x => new ValueLevel { MethodId = 0, MethodUniqueId = string.Empty })
                .Where(x => x.ProjectId == methodDto.ProjectId && x.CdiscDataType == methodDto.CdiscDataType && x.MethodId == methodDto.Id)
                .ExecuteCommandAsync();
        }
        var result = await sqlSugar.Deleteable(mapper.Map<Method>(methodDto)).ExecuteCommandAsync();
        lookupStore.RemoveMethod(methodDto.Id);
        return result;
    }

    public async Task<int> UpdateMethodAsync(MethodDto methodDto)
    {
        var method = mapper.Map<Method>(methodDto);
        var result = await sqlSugar.Updateable(method).ExecuteCommandAsync();
        lookupStore.UpsertMethod(method);
        return result;
    }

    public async Task<MethodDto> InsertMethodAsync(Method method)
    {
        var entity = await sqlSugar.Insertable(method).ExecuteReturnEntityAsync();
        lookupStore.UpsertMethod(entity);
        return mapper.Map<MethodDto>(entity);
    }

    public async Task<MethodDto> InsertMethodAsync(MethodDto methodDto, bool linkMatchingVariables, string? variableMatchMode, string? variableMatchText)
    {
        var method = mapper.Map<Method>(methodDto);
        var insertedMethod = await InsertMethodAsync(method);
        if (linkMatchingVariables &&
            !string.IsNullOrWhiteSpace(variableMatchMode) &&
            !string.IsNullOrWhiteSpace(variableMatchText))
        {
            await variableService.LinkMethodToMatchingVariablesAsync(
                mapper.Map<Method>(insertedMethod),
                variableMatchMode,
                variableMatchText);
        }

        return insertedMethod;
    }

    public async Task<int> InitializeTemplateMethodsAsync(IReadOnlyCollection<Variable> importedVariables)
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        if (projectId == 0 || importedVariables.Count == 0)
            return 0;

        var importedKeys = importedVariables
            .Where(variable => !string.IsNullOrWhiteSpace(variable.DatasetName) &&
                               !string.IsNullOrWhiteSpace(variable.VariableName))
            .Select(variable => $"{variable.DatasetName}\u001f{variable.VariableName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variableNames = importedVariables
            .Where(variable => !string.IsNullOrWhiteSpace(variable.VariableName))
            .Select(variable => variable.VariableName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (importedKeys.Count == 0 || variableNames.Count == 0)
            return 0;

        var templates = await sqlSugar.AsTenant().QueryableWithAttr<TemplateMethod>()
            .Where(template => template.CdiscDataType == dataType &&
                               template.UniqueId != null &&
                               variableNames.Contains(template.UniqueId))
            .ToListAsync();
        if (templates.Count == 0)
            return 0;

        var templateByUniqueId = templates
            .Where(template => !string.IsNullOrWhiteSpace(template.UniqueId))
            .GroupBy(template => template.UniqueId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var templateUniqueIds = templateByUniqueId.Keys.ToList();

        var variables = (await sqlSugar.Queryable<Variable>()
                .Where(variable => variable.ProjectId == projectId &&
                                   variable.CdiscDataType == dataType &&
                                   variable.MethodId == 0 &&
                                   variable.VariableName != null &&
                                   templateUniqueIds.Contains(variable.VariableName))
                .ToListAsync())
            .Where(variable => !string.IsNullOrWhiteSpace(variable.DatasetName) &&
                               !string.IsNullOrWhiteSpace(variable.VariableName) &&
                               importedKeys.Contains($"{variable.DatasetName}\u001f{variable.VariableName}"))
            .ToList();
        if (variables.Count == 0)
            return 0;

        var existingMethods = await sqlSugar.Queryable<Method>()
            .Where(method => method.ProjectId == projectId &&
                             method.CdiscDataType == dataType &&
                             method.UniqueId != null &&
                             templateUniqueIds.Contains(method.UniqueId))
            .ToListAsync();
        var methodsByUniqueId = existingMethods
            .Where(method => !string.IsNullOrWhiteSpace(method.UniqueId))
            .GroupBy(method => method.UniqueId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var template in templateByUniqueId.Values)
        {
            var uniqueId = template.UniqueId!;
            if (methodsByUniqueId.ContainsKey(uniqueId))
                continue;

            var method = await sqlSugar.Insertable(new Method
            {
                UniqueId = uniqueId,
                Name = template.Name,
                Type = template.Type,
                Description = template.Description,
                ExpressionContext = template.ExpressionContext,
                ExpressionCode = template.ExpressionCode,
                ProjectId = projectId,
                CdiscDataType = dataType
            }).ExecuteReturnEntityAsync();
            methodsByUniqueId[uniqueId] = method;
            lookupStore.UpsertMethod(method);
        }

        foreach (var variable in variables)
        {
            var method = methodsByUniqueId[variable.VariableName!];
            variable.MethodId = method.Id;
            variable.MethodUniqueId = method.UniqueId;
            variable.Method = method;
        }

        await sqlSugar.Utilities.PageEachAsync(variables, 200, async page =>
        {
            await sqlSugar.Updateable(page).ExecuteCommandAsync();
        });

        return variables.Count;
    }

    public async Task<int> SaveMethodsAsync(List<MethodDto> methods)
    {
        var list = mapper.Map<List<Method>>(methods);
        var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        await issueService.SyncIssuesAsync(methods, nameof(MethodDto), dto=>dto.Id);
        await lookupStore.RefreshAsync(LookupKind.Method);
        return inserted + updated;
    }
}
