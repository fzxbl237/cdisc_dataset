using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public class Definition
{
    public enum Target
    {
        Domain,
        Filter,
        Rule,
        Variable
    }

    protected readonly KeyMap<string> Properties = new();
    protected readonly HashSet<Definition> Dependencies = new();
    private readonly HashSet<string> _usedPrefixes = new(StringComparer.OrdinalIgnoreCase);

    private readonly Target _target;
    private readonly string _targetName;
    private string _prefix;

    public Definition(Target target, string targetName)
        : this(target, targetName, string.Empty)
    {
    }

    public Definition(Target target, string targetName, string prefix)
    {
        _target = target;
        _targetName = targetName;
        SetPrefix(prefix);
    }

    public static void CopyTo(Definition source, Definition destination)
    {
        CopyTo(source, destination, false);
    }

    public static void CopyTo(Definition source, Definition destination, bool stripPrefixes)
    {
        CopyTo(source, destination, stripPrefixes, null);
    }

    public static void CopyTo(Definition source, Definition destination, params string[]? forceCopy)
    {
        CopyTo(source, destination, false, forceCopy);
    }

    public static void CopyTo(Definition source, Definition destination, bool stripPrefixes, params string[]? forceCopy)
    {
        var forced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (forceCopy != null)
        {
            foreach (var property in forceCopy)
            {
                forced.Add(property.ToUpperInvariant());
            }
        }

        foreach (var property in source.GetProperties())
        {
            var targetProperty = property;
            if (stripPrefixes)
            {
                var index = property.IndexOf('.');
                if (index != -1)
                {
                    var prefix = property[..index];
                    if (source._usedPrefixes.Contains(prefix))
                    {
                        targetProperty = property[(index + 1)..];
                    }
                }
            }

            if (!destination.HasProperty(targetProperty) || forced.Contains(property))
            {
                destination.SetProperty(targetProperty, source.GetProperty(property));
            }
        }

        foreach (var dependency in source.Dependencies)
        {
            destination.Dependencies.Add(dependency);
        }
    }

    public static Definition CreateFrom(Definition definition)
    {
        return CreateFrom(definition.GetTargetName(), definition);
    }

    public static Definition CreateFrom(params Definition[] definitions)
    {
        return CreateFrom(definitions[0].GetTargetName(), definitions);
    }

    public static Definition CreateFrom(string targetName, params Definition[] definitions)
    {
        return CreateFrom(targetName, false, definitions);
    }

    public static Definition CreateFrom(string targetName, bool stripPrefixes, params Definition[] definitions)
    {
        var target = definitions[0].GetTarget();
        var combination = new Definition(target, targetName);

        foreach (var definition in definitions)
        {
            if (definition.GetTarget() != target)
            {
                throw new ArgumentException("Definitions of varying targets cannot be combined");
            }

            CopyTo(definition, combination, stripPrefixes);
        }

        return combination;
    }

    internal Definition With(string prefix, Definition definition)
    {
        var target = CreateFrom(this);
        target.SetPrefix(prefix);
        CopyTo(definition, target);
        target.ClearPrefix();
        return target;
    }

    internal bool HasDependencies() => Dependencies.Count > 0;

    internal Definition AddDependency(Definition dependency)
    {
        Dependencies.Add(dependency);
        return this;
    }

    public void ClearPrefix() => SetPrefix(string.Empty);

    public IReadOnlyCollection<Definition> GetDependencies() => Dependencies.ToList();

    public string GetPrefix() => _prefix;

    public string GetProperty(string property)
    {
        return HasProperty(property) ? Properties.Get(Prefix(property)) ?? string.Empty : string.Empty;
    }

    public IReadOnlyCollection<string> GetProperties() => Properties.KeySet();

    public Target GetTarget() => _target;

    public string GetTargetName() => _targetName;

    public bool HasProperty(string property) => Properties.ContainsKey(Prefix(property));

    public void SetPrefix(string prefix)
    {
        _prefix = prefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            _usedPrefixes.Add(prefix.ToUpperInvariant());
        }
    }

    public Definition SetProperty(string property, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Properties.Put(Prefix(property), value);
        }

        return this;
    }

    public override string ToString()
    {
        return $"{_target} {_targetName} [{string.Join(", ", Properties.KeySet())}]";
    }

    private string Prefix(string property)
    {
        return string.IsNullOrEmpty(_prefix) ? property : $"{_prefix}.{property}";
    }
}
