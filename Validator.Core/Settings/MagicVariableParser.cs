using System.Text.RegularExpressions;
using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

internal sealed class MagicVariableParser
{
    public enum MagicProperty
    {
        Variables,
        Domains
    }

    public class MagicPropertyItem
    {
        private readonly MagicProperty  _magicProperty;

        private readonly List<string> _properties = [];

        public MagicPropertyItem(MagicProperty magicProperty)
        {
            this._magicProperty = magicProperty;
            switch (_magicProperty)
            {
                case MagicProperty.Variables:
                    _properties.AddRange(["If","Terms","Test","Variable","When","GroupBy"]);
                    break;
                case MagicProperty.Domains:
                    _properties.AddRange(["From","Terms"]);
                    break;
            }
        }

        public string[] GetProperties()
        {
            return _properties.ToArray();
        }

        public string GetSingular()
        {
            switch (_magicProperty)
            {
                case MagicProperty.Variables:
                   return "Variable";
                case MagicProperty.Domains:
                    return "Domain";
            }
            return string.Empty;
        }
    }

    private const string ContextProperty = "VariableContext";

    private readonly Regex _clause = new("[A-Za-z0-9!,;*#@_]+", RegexOptions.Compiled);
    private readonly Regex _token = new("(?:^| |'|\\()([=+]?%[^%]+%)", RegexOptions.Compiled);

    public List<MagicVariable> Parse(MagicProperty property, string name, string parsable)
    {
        var tokenizer = _token.Matches(parsable);
        var magicVariables = new List<MagicVariable>();

        foreach (Match match in tokenizer)
        {
            var magicName = property.ToString();
            var original = match.Groups[1].Value;
            var token = original[(original.IndexOf('%') + 1)..^1];

            if (token.StartsWith(magicName, StringComparison.Ordinal))
            {
                token = token[magicName.Length..];
                var tokens = token.Split('$');
                var conditions = new List<string>();
                var clauses = new List<List<string>>();

                var isScoped = tokens[0].StartsWith(':');
                if (isScoped)
                {
                    var end = tokens[0].IndexOf('[', StringComparison.Ordinal);
                    if (end == -1)
                    {
                        end = tokens[0].IndexOf('.', StringComparison.Ordinal);
                        if (end == -1)
                        {
                            end = tokens[0].Length;
                        }
                    }

                    conditions.Add(tokens[0].Substring(1, end - 1));
                    tokens[0] = tokens[0][end..];
                }

                if (tokens[0].StartsWith("["))
                {
                    var end = tokens[0].IndexOf(']');
                    if (end != -1)
                    {
                        var clause = tokens[0].Substring(1, end - 1);
                        if (_clause.IsMatch(clause))
                        {
                            tokens[0] = tokens[0][(end + 1)..];
                            var pieces = clause.Split(';');
                            foreach (var piece in pieces)
                            {
                                clauses.Add(piece.Split(',').ToList());
                            }
                        }
                        else
                        {
                            throw new MagicVariableSyntaxException(name, original, "Invalid variable clause syntax");
                        }
                    }
                    else
                    {
                        throw new MagicVariableSyntaxException(name, original, "Missing closing ]");
                    }
                }

                if (tokens[0].StartsWith("."))
                {
                    tokens[0] = tokens[0][1..];
                }

                foreach (var identifier in tokens)
                {
                    if (identifier.Length > 0)
                    {
                        if (identifier.Contains("Define", StringComparison.Ordinal))
                        {
                            isScoped = true;
                        }

                        conditions.Add(identifier);
                    }
                }

                if (!isScoped)
                {
                    conditions.Add("Config");
                }

                magicVariables.Add(new MagicVariable(property, original, clauses, conditions));
            }
        }

        return magicVariables;
    }

    public List<RuleDefinition> Prepare(RuleDefinition rule, MagicVariable magicVariable, List<Definition> candidateVariables, Dictionary<string, string> defaults)
    {
        var preparedRules = new List<RuleDefinition>();

        if (magicVariable.IsReplicated())
        {
            foreach (var candidateVariable in candidateVariables)
            {
                var normalizedCandidates = new List<Definition>();
                if (magicVariable.IsDependencyReplicated())
                {
                    foreach (var dependency in candidateVariable.GetDependencies())
                    {
                        if (magicVariable.MatchesClause(dependency))
                        {
                            var prefix = dependency.GetProperty("Prefix");
                            var context = Hex.Sha1(dependency.GetProperty("Expression"))
                                .Substring(0, 6)
                                .ToUpperInvariant();

                            if (!string.IsNullOrEmpty(prefix))
                            {
                                prefix += ".";
                            }

                            normalizedCandidates.Add(candidateVariable.With(prefix + "@Clause", dependency)
                                .SetProperty(ContextProperty, context));
                        }
                    }
                }
                else
                {
                    normalizedCandidates.Add(candidateVariable);
                }

                foreach (var normalizedCandidate in normalizedCandidates)
                {
                    var contextualRule = rule.WithContext(normalizedCandidate);
                    var name = normalizedCandidate.GetTargetName().ToUpperInvariant();
                    var context = name;

                    if (normalizedCandidate.HasProperty(ContextProperty))
                    {
                        context += "." + normalizedCandidate.GetProperty(ContextProperty);
                    }

                    preparedRules.Add(ReplaceProperties(
                        ReplaceVariable(contextualRule, magicVariable, name, context),
                        magicVariable,
                        normalizedCandidate,
                        defaults
                    ));
                }
            }
        }
        else
        {
            var builder = new System.Text.StringBuilder();
            var contextualRule = rule;
            Definition? previous = null;

            foreach (var candidate in candidateVariables)
            {
                if (previous != null)
                {
                    var name = previous.GetTargetName().ToUpperInvariant();
                    builder.Append(name).Append(',');
                }

                previous = candidate;
                contextualRule = contextualRule.WithContext(candidate);
            }

            if (previous != null)
            {
                builder.Append(previous.GetTargetName().ToUpperInvariant());
            }

            preparedRules.Add(ReplaceVariable(contextualRule, magicVariable, builder.ToString(), null));
        }

        return preparedRules;
    }

    private RuleDefinition ReplaceProperties(RuleDefinition rule, MagicVariable magicVariable, Definition candidateVariable, Dictionary<string, string> defaults)
    {
        var magicProperty = magicVariable.GetMagicProperty();
        var token = new Regex("%" + magicProperty.GetSingular() + "\\.([^%|]+)(?:\\|([^%]+))?%", RegexOptions.Compiled);

        foreach (var property in rule.GetProperties())
        {
            var value = rule.GetProperty(property);
            var matches = token.Matches(value).ToList();

            foreach (var match in matches)
            {
                var expected = match.Groups[1].Value;
                var fallback = match.Groups[2].Value;

                if (candidateVariable.HasProperty(expected))
                {
                    value = value.Replace(match.Value, candidateVariable.GetProperty(expected));
                    continue;
                }

                if (!string.IsNullOrEmpty(fallback))
                {
                    string? replacement = null;
                    if (fallback.StartsWith("System.", StringComparison.Ordinal))
                    {
                        var key = fallback["System.".Length..].ToLowerInvariant();
                        defaults.TryGetValue(key, out replacement);
                    }
                    else if (candidateVariable.HasProperty(fallback))
                    {
                        replacement = candidateVariable.GetProperty(fallback);
                    }

                    if (replacement != null)
                    {
                        value = value.Replace(match.Value, replacement);
                    }
                }
            }

            var references = magicVariable.GetReferences(candidateVariable);
            if (references != null)
            {
                foreach (var reference in references)
                {
                    value = value.Replace("%" + magicProperty.GetSingular() + "." + reference.Key + "%", reference.Value);
                }
            }

            value = value.Replace("%" + magicProperty.GetSingular() + "%", candidateVariable.GetTargetName());
            rule = rule.WithProperty(property, value);
        }

        return rule;
    }

    private RuleDefinition ReplaceVariable(RuleDefinition rule, MagicVariable magicVariable, string replacement, string? context)
    {
        foreach (var property in magicVariable.GetMagicProperty().GetProperties())
        {
            if (rule.HasProperty(property))
            {
                rule = rule.WithProperty(property, rule.GetProperty(property).Replace(magicVariable.GetIdentifier(), replacement));
            }
        }

        if (context != null)
        {
            rule = rule.WithContext(context);
        }

        return rule;
    }
}
