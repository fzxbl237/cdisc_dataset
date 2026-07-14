using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Services.Interface;

public interface IValueLevelService
{
    Task<List<ValueLevelDto>> GetAllValueLevelDtosAsync();
    Task<List<ValueLevelDto>> GetAllValueLevelDtosWithoutErorrAsync();
    Task<List<ValueLevel>> GetAllValueLevelsWithoutErorrAsync();
    Task<int> DeleteValueLevelAsync(ValueLevelDto valueLevelDto);
    Task<int> UpdateValueLevelAsync(ValueLevelDto valueLevelDto);
    Task<ValueLevelDto> InsertValueLevelAsync(ValueLevel valueLevel);
    Task<ValueLevelDto> InsertValueLevelAsync(ValueLevelDto valueLevelDto);
    Task<bool> SaveValueLevelsAsync(List<ValueLevelDto> valueLevels);
}
