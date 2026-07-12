using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface IValueLevelService
{
    Task<List<ValueLevelDto>> GetAllValueLevelDtosAsync(int projectId, CdiscDataType dataType);
    Task<List<ValueLevelDto>> GetAllValueLevelDtosWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<List<ValueLevel>> GetAllValueLevelsWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<int> DeleteValueLevelAsync(ValueLevelDto valueLevelDto);
    Task<int> UpdateValueLevelAsync(ValueLevelDto valueLevelDto);
    Task<ValueLevelDto> InsertValueLevelAsync(ValueLevel valueLevel);
    Task<ValueLevelDto> InsertValueLevelAsync(ValueLevelDto valueLevelDto);
    Task<bool> SaveValueLevelsAsync(List<ValueLevelDto> valueLevels);
}
