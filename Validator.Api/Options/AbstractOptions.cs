namespace P21.Validator.Api.Options;

public abstract class AbstractOptions
{
    private readonly Dictionary<string, string> _properties = new(StringComparer.OrdinalIgnoreCase);

    protected AbstractOptions(IDictionary<string, string> properties)
    {
        foreach (var pair in properties)
        {
            _properties[pair.Key] = pair.Value;
        }
    }

    public string GetProperty(string property)
    {
        return _properties.TryGetValue(property, out var value) ? value : string.Empty;
    }

    public IReadOnlyCollection<string> GetProperties()
    {
        return _properties.Keys.ToList();
    }

    public bool HasProperty(string property)
    {
        return !string.IsNullOrEmpty(GetProperty(property));
    }

    public bool HasProperty(string property, string value)
    {
        return string.Equals(GetProperty(property), value, StringComparison.OrdinalIgnoreCase);
    }

    public abstract class AbstractBuilder<TBuilder> where TBuilder : AbstractBuilder<TBuilder>
    {
        internal Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TBuilder WithProperty(string property, string value)
        {
            Properties[property] = value;
            return (TBuilder)this;
        }
    }
}
