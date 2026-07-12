namespace P21.Validator.Api.Options;

public class PropertySet<TProperty> where TProperty : struct, Enum
{
    private readonly Dictionary<TProperty, object> _properties = new();

    public PropertySet(IDictionary<TProperty, object> values)
    {
        foreach (var pair in values)
        {
            _properties[pair.Key] = pair.Value;
        }
    }

    public bool HasProperty(TProperty property)
    {
        return _properties.ContainsKey(property);
    }

    public string GetString(TProperty property)
    {
        if (!HasProperty(property))
        {
            throw new ArgumentException($"The property {property} is not set");
        }

        return _properties[property].ToString() ?? string.Empty;
    }

    public string? GetString(TProperty property, string? defaultValue)
    {
        return HasProperty(property) ? _properties[property].ToString() : defaultValue;
    }

    public void SetProperty(TProperty property, object value)
    {
        _properties[property] = value;
    }
}
