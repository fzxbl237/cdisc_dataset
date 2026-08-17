using System.Threading.Tasks;
using cdisc_dataset.Models;

namespace cdisc_dataset.Services.Interface;

public interface IDefineXmlValidationService
{
    Task<DefineXmlValidationResult> ValidateAsync();
}
