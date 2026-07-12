using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public class PropertySet<TProperty> where TProperty : struct, Enum
{
    protected readonly Dictionary<TProperty, object?> Properties;
    private readonly HashSet<TProperty> _updatable;

    protected PropertySet(IEnumerable<TProperty> updatable)
    {
        Properties = new Dictionary<TProperty, object?>();
        _updatable = new HashSet<TProperty>(updatable);
    }

    protected PropertySet() : this(Array.Empty<TProperty>())
    {
    }

    public bool GetBoolean(TProperty property)
    {
        return (Convert(property) ?? throw UnsetException(property)) != 0;
    }

    public bool GetBoolean(TProperty property, bool defaultValue)
    {
        var converted = Convert(property);
        return converted.HasValue ? converted.Value != 0 : defaultValue;
    }

    public bool? GetBoolean(TProperty property, bool? defaultValue)
    {
        var converted = Convert(property);
        return converted.HasValue ? converted.Value != 0 : defaultValue;
    }

    public int GetInteger(TProperty property)
    {
        return Convert(property) ?? throw UnsetException(property);
    }

    public int? GetInteger(TProperty property, int? defaultValue)
    {
        return Convert(property) ?? defaultValue;
    }

    public string GetString(TProperty property)
    {
        if (!HasProperty(property))
        {
            throw UnsetException(property);
        }

        return Properties[property]?.ToString() ?? string.Empty;
    }

    public string? GetString(TProperty property, string? defaultValue)
    {
        return Properties.TryGetValue(property, out var value)
            ? value?.ToString()
            : defaultValue;
    }

    public bool HasProperty(TProperty property)
    {
        return Properties.ContainsKey(property);
    }

    public void SetProperty(TProperty property, object? value)
    {
        if (HasProperty(property) && !_updatable.Contains(property))
        {
            throw new ArgumentException($"Cannot reassign the property {property}");
        }

        if (value is null)
        {
            return;
        }

        if (value is string text && text.Length == 0)
        {
            return;
        }

        Properties[property] = value;
    }

    private int? Convert(TProperty property)
    {
        if (property is not SourceDetails.Property && property is not VariableDetails.Property)
        {
            return null;
        }

        var isConvertible = property switch
        {
            SourceDetails.Property sourceProperty => sourceProperty.IsConvertible(),
            VariableDetails.Property variableProperty => variableProperty switch
            {
                VariableDetails.Property.Length => true,
                VariableDetails.Property.Order => true,
                _ => false
            },
            _ => false
        };

        if (!isConvertible)
        {
            throw new ArgumentException($"The property {property} cannot be converted to a non-String type");
        }

        if (!Properties.TryGetValue(property, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            bool boolValue => boolValue ? 1 : 0,
            _ => int.TryParse(value.ToString(), out var converted) ? converted : null
        };
    }

    private static ArgumentException UnsetException(object property)
    {
        return new ArgumentException($"The property {property} is not set");
    }
}
