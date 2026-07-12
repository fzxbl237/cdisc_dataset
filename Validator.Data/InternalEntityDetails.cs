using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public sealed class InternalEntityDetails : PropertySet<SourceDetails.Property>, SourceDetails
{
    private static readonly SourceDetails.Property[] UpdatableProperties =
    {
        SourceDetails.Property.Filtered,
        SourceDetails.Property.Location,
        SourceDetails.Property.Records,
        SourceDetails.Property.Variables
    };

    private readonly SourceDetails.Reference _reference;
    private readonly SourceDetails? _parent;
    private readonly List<SourceDetails> _subentities = new();
    private readonly List<VariableDetails> _variables = new();
    private readonly Dictionary<string, int> _lookup = new(StringComparer.OrdinalIgnoreCase);

    public InternalEntityDetails(SourceDetails.Reference reference, string name, string? subname, string location)
        : base(UpdatableProperties)
    {
        if (name == null)
        {
            throw new ArgumentException("name cannot be null", nameof(name));
        }

        if (location == null)
        {
            throw new ArgumentException("location cannot be null", nameof(location));
        }

        _reference = reference;
        SetProperty(SourceDetails.Property.Name, name.ToUpperInvariant());
        SetProperty(SourceDetails.Property.Location, location);
        _parent = null;

        if (!string.IsNullOrEmpty(subname))
        {
            SetProperty(SourceDetails.Property.Subname, subname.ToUpperInvariant());
        }
    }

    public InternalEntityDetails(SourceDetails.Reference reference, IDictionary<SourceDetails.Property, object> properties, InternalEntityDetails? parent)
        : base(UpdatableProperties)
    {
        if (properties == null)
        {
            throw new ArgumentException("properties map cannot be null", nameof(properties));
        }

        if (!properties.ContainsKey(SourceDetails.Property.Name))
        {
            throw new ArgumentException("the Name property must always be specified");
        }

        _reference = reference;
        foreach (var pair in properties)
        {
            Properties[pair.Key] = pair.Value;
        }

        _parent = parent;
    }

    public InternalEntityDetails(SourceDetails.Reference reference, InternalEntityDetails template)
        : this(reference, template.Properties, null)
    {
    }

    public InternalEntityDetails(SourceDetails.Reference reference, InternalEntityDetails template, InternalEntityDetails? parent)
        : this(reference, template.Properties, parent)
    {
    }

    public SourceDetails? GetParent() => _parent;

    public SourceDetails.Reference GetReference() => _reference;

    public bool HasVariable(string name) => GetVariable(name) != null;

    public VariableDetails? GetVariable(string name)
    {
        return _lookup.TryGetValue(name, out var position) ? _variables[position] : null;
    }

    public IReadOnlyList<VariableDetails> GetVariables() => _variables.AsReadOnly();

    public void AddVariable(VariableDetails variable)
    {
        _lookup[variable.GetString(VariableDetails.Property.Name)] = _variables.Count;
        _variables.Add(variable);
    }

    public void AddSubentity(InternalEntityDetails subentity)
    {
        _subentities.Add(subentity);
    }

    public IReadOnlyList<SourceDetails> GetSplitSources() => _subentities.AsReadOnly();
}
