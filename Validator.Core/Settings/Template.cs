using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public sealed class Template : Definition
{
    private static readonly Dictionary<string, string> TemplateVariableMappings = new(StringComparer.Ordinal)
    {
        ["*"] = ".+",
        ["#"] = "\\d+",
        ["@"] = "[A-Za-z]+"
    };

    private bool _configured;
    private bool _defined;
    private readonly Dictionary<SourceDetails.Reference, HashSet<RuleDefinition>> _rules = new();
    private readonly HashSet<Definition> _templateVariables = new();
    private readonly KeyMap<Definition> _variables = new();
    private PrototypeCriteria? _prototypeCriteria;

    public Template(string name, PrototypeCriteria? prototypeCriteria)
        : base(Target.Domain, name)
    {
        _prototypeCriteria = prototypeCriteria;

        foreach (var target in Enum.GetValues<SourceDetails.Reference>())
        {
            _rules[target] = new HashSet<RuleDefinition>();
        }
    }

    public Configuration CreateFrom(string name, HashSet<string>? existing)
    {
        var configuration = new Configuration(name);

        if (existing != null)
        {
            var variables = new HashSet<string>(existing.Select(v => v.ToUpperInvariant()));

            foreach (var variable in _variables.Values())
            {
                var variableName = Prepare(variable.GetTargetName(), name);
                var replacementVariable = CreateFrom(variableName, variable);

                if (variables.Contains(variableName))
                {
                    replacementVariable.SetProperty("Present", "Y");
                }

                configuration.DefineVariable(replacementVariable);
                variables.Remove(variableName);
            }

            if (_templateVariables.Count > 0)
            {
                foreach (var variable in variables)
                {
                    var template = FindVariableMatch(variable);
                    if (template != null)
                    {
                        var replacementVariable = CreateFrom(variable, template.Template);
                        if (replacementVariable.HasProperty("Name"))
                        {
                            replacementVariable.SetProperty("Name", variable);
                        }

                        replacementVariable.SetProperty("Present", "Y");
                        if (replacementVariable.HasProperty("Label"))
                        {
                            var label = replacementVariable.GetProperty("Label");
                            for (var i = 1; i <= template.Captures.Length; ++i)
                            {
                                label = Regex.Replace(label, Regex.Escape("$" + i), template.Captures[i - 1]);
                            }

                            replacementVariable.SetProperty("Label", label);
                        }

                        configuration.DefineVariable(replacementVariable);
                    }
                }
            }
        }

        foreach (var property in Properties.EntrySet())
        {
            configuration.SetProperty(property.Key, property.Value);
        }

        if (_prototypeCriteria != null)
        {
            var label = HasProperty("Label") ? GetProperty("Label") : name;
            var keys = GetProperty("DomainKeys");

            configuration.SetProperty("Name", name);
            configuration.SetProperty("Label", label.Replace("%Domain%", name, StringComparison.OrdinalIgnoreCase));
            configuration.SetProperty("DomainKeys", keys.Replace("__", name, StringComparison.OrdinalIgnoreCase));
        }

        return configuration;
    }

    public void Complete()
    {
        if (_templateVariables.Count == 0)
        {
            return;
        }

        foreach (var variable in _variables.Values())
        {
            if (!variable.HasProperty("Config"))
            {
                var template = FindVariableMatch(variable.GetTargetName());
                if (template != null)
                {
                    variable.SetProperty("Config", "Y");
                    foreach (var property in template.Template.GetProperties())
                    {
                        if (!variable.HasProperty(property))
                        {
                            variable.SetProperty(property, template.Template.GetProperty(property));
                        }
                    }

                    if (variable.HasProperty("Label"))
                    {
                        var label = variable.GetProperty("Label");
                        for (var i = 1; i <= template.Captures.Length; ++i)
                        {
                            label = Regex.Replace(label, Regex.Escape("$" + i), template.Captures[i - 1]);
                        }

                        variable.SetProperty("Label", label);
                    }
                }
            }
        }
    }

    public void DefineRule(SourceDetails.Reference target, RuleDefinition rule)
    {
        if (!ConfigurationManager.IsValidRuleType(rule.GetRuleType()))
        {
            return;
        }

        _rules[target].Add(rule);
    }

    public void DefinePrototypeCriteria(PrototypeCriteria prototypeCriteria)
    {
        _prototypeCriteria = prototypeCriteria;
    }

    public void DefineVariable(Definition variable)
    {
        var variableName = variable.GetTargetName();
        foreach (var templateSequence in TemplateVariableMappings.Keys)
        {
            if (variableName.Contains(templateSequence, StringComparison.Ordinal))
            {
                _templateVariables.Add(variable);
                return;
            }
        }

        _variables.Put(variableName, variable);
    }

    public IReadOnlyCollection<RuleDefinition> GetRules(SourceDetails.Reference target) => _rules[target];

    public Definition? GetVariable(string name) => _variables.Get(name);

    public bool HasVariable(string name) => _variables.ContainsKey(name);

    public bool IsConfiguration() => _configured;

    public bool IsDefinition() => _defined;

    public int Matches(string name, ICollection<string> datasetVariables)
    {
        var matches = 0;
        if (_prototypeCriteria?.IsFallbackCriteria() == true)
        {
            matches = -1;
        }
        else if (_prototypeCriteria != null)
        {
            if (_prototypeCriteria.HasDatasetName())
            {
                if (Regex.IsMatch(name, _prototypeCriteria.GetDatasetName().Replace("*", ".*")))
                {
                    matches = 1;
                }
            }

            if (_prototypeCriteria.HasVariables())
            {
                var normalized = datasetVariables.Select(v => v.Trim().ToUpperInvariant()).ToHashSet();
                foreach (var variable in _prototypeCriteria.GetVariables())
                {
                    var isNegativeMatch = variable.StartsWith('-');
                    var isRegex = variable.Contains('*');
                    var raw = isNegativeMatch ? variable[1..] : variable;
                    raw = Prepare(raw, name);

                    var isFound = isRegex
                        ? normalized.Any(s => Regex.IsMatch(s, raw.Replace("*", ".*")))
                        : normalized.Contains(raw);

                    if (isFound)
                    {
                        if (isNegativeMatch)
                        {
                            return 0;
                        }

                        matches += 10;
                    }
                }
            }
        }

        return matches;
    }

    public void MarkConfigured() => _configured = true;

    public void MarkDefined() => _defined = true;

    public void UpdateVariables(Template target)
    {
        var targetName = target.GetProperty("Name").ToUpperInvariant();

        foreach (var variable in _variables.Values())
        {
            var name = Prepare(variable.GetTargetName(), targetName);
            if (target.HasVariable(name))
            {
                CopyTo(variable, target.GetVariable(name)!, false);
            }
            else
            {
                target.DefineVariable(CreateFrom(name, variable));
            }
        }

        foreach (var variable in _templateVariables)
        {
            var name = Prepare(variable.GetTargetName(), targetName);
            target.DefineVariable(CreateFrom(name, variable));
        }
    }

    private string Prepare(string variable, string name)
    {
        return variable.Trim().Replace("__", name).ToUpperInvariant();
    }

    private VariableTemplate? FindVariableMatch(string variable)
    {
        var matches = 0;
        var captured = int.MaxValue;
        Definition? current = null;
        string[]? captures = null;

        foreach (var templateVariable in _templateVariables)
        {
            var pattern = templateVariable.GetTargetName();
            foreach (var replacementSequence in TemplateVariableMappings)
            {
                pattern = pattern.Replace(replacementSequence.Key, "(" + replacementSequence.Value + ")");
            }

            var matcher = Regex.Match(variable, pattern);
            if (matcher.Success)
            {
                var count = matcher.Groups.Count - 1;
                var length = 0;
                var groups = new string[count];

                for (var i = 1; i <= count; ++i)
                {
                    groups[i - 1] = matcher.Groups[i].Value;
                    length += groups[i - 1].Length;
                }

                if (count > matches || (count == matches && length < captured))
                {
                    matches = count;
                    captured = length;
                    current = templateVariable;
                    captures = groups;
                }
            }
        }

        return current == null || captures == null ? null : new VariableTemplate(current, captures);
    }

    private sealed class VariableTemplate
    {
        public VariableTemplate(Definition template, string[] captures)
        {
            Template = template;
            Captures = captures;
        }

        public Definition Template { get; }
        public string[] Captures { get; }
    }
}
