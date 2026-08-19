using System.Threading.Tasks;

namespace PatChes.Services.Interface;

public interface ISettingsService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
    Task SaveAsync();
}