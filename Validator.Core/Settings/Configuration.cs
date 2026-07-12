using P21.Validator.Api.Models;
using P21.Validator.Core.Rules;
using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public sealed class Configuration : Definition
{
    private readonly HashSet<ValidationRule> _filters = new();
    private readonly Dictionary<SourceDetails.Reference, HashSet<ValidationRule>> _rules = new();
    private readonly KeyMap<Definition> _variables = new();

    public Configuration(string name)
        : base(Target.Domain, name)
    {
        foreach (var target in Enum.GetValues<SourceDetails.Reference>())
        {
            _rules[target] = new HashSet<ValidationRule>();
        }
    }

    public IReadOnlyCollection<ValidationRule> GetRules(SourceDetails.Reference target) => _rules[target];

    public IReadOnlyCollection<ValidationRule> GetFilters() => _filters;

    public Definition? GetVariable(string variable) => _variables.Get(variable);

    public IReadOnlyCollection<Definition> GetVariables() => _variables.Values().ToList();

    public bool HasVariable(string name) => _variables.ContainsKey(name);

    internal void DefineRule(SourceDetails.Reference target, ValidationRule rule)
    {
        _rules[target].Add(rule);
    }

    internal void DefineFilter(ValidationRule filter)
    {
        _filters.Add(filter);
    }

    internal void DefineVariable(Definition variable)
    {
        _variables.Put(variable.GetTargetName(), variable);
    }
}
