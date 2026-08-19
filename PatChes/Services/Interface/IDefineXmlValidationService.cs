using System.Threading.Tasks;
using PatChes.Models;

namespace PatChes.Services.Interface;

public interface IDefineXmlValidationService
{
    Task<DefineXmlValidationResult> ValidateAsync();
}
