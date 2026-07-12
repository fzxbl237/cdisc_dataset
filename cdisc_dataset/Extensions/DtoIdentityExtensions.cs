using System.Reflection;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Extensions;

public static class DtoIdentityExtensions
{
    public static string? GetEntityId(this BaseDto dto)
    {
        var dtoType = dto.GetType();

        var stringProps = new[] { "UniqueId", "Uuid" };
        foreach (var propName in stringProps)
        {
            var property = dtoType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property?.GetValue(dto) is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var idProperty = dtoType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        var idValue = idProperty?.GetValue(dto);
        return idValue switch
        {
            null => null,
            int intValue when intValue > 0 => intValue.ToString(),
            long longValue when longValue > 0 => longValue.ToString(),
            _ => null
        };
    }
}
