using System.Text.RegularExpressions;
using static P21.Validator.Core.Settings.MagicVariableParser;

namespace P21.Validator.Core.Settings;

internal sealed class MagicVariable
{
    private readonly List<List<string>> _clauses;
    private readonly List<string> _conditions = new();
    private readonly List<string> _clauseConditions = new();
    private readonly Regex _grouping = new("#+|@+|_+", RegexOptions.Compiled);
    private readonly string _identifier;
    private readonly bool _isReplicated;
    private readonly bool _isDependencyReplicated;
    private readonly MagicProperty _magicProperty;
    private readonly Dictionary<string, Dictionary<int, string>> _references = new(StringComparer.OrdinalIgnoreCase);

    public MagicVariable(MagicProperty magicProperty, string identifier, List<List<string>> clauses, List<string> conditions)
    {
        _clauses = clauses;
        _identifier = identifier;
        _isReplicated = !_identifier.StartsWith("=", StringComparison.Ordinal);
        _isDependencyReplicated = _identifier.StartsWith("+", StringComparison.Ordinal);
        _magicProperty = magicProperty;

        foreach (var condition in conditions)
        {
            var pieces = condition.Split(':', 2);
            var clauseIndex = pieces[0].IndexOf("@Clause", StringComparison.Ordinal);
            var dotIndex = pieces[0].IndexOf('.', clauseIndex==-1?0:clauseIndex);

            var currentCondition = condition;
            if (clauseIndex > -1)
            {
                if (_clauseConditions.Count == 0)
                {
                    var prefix = currentCondition[..clauseIndex];
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        prefix = ":" + prefix;
                        if (prefix.EndsWith(".", StringComparison.Ordinal))
                        {
                            prefix = prefix[..^1];
                        }
                    }

                    _clauseConditions.Add((clauseIndex == 0 ? "!" : string.Empty) + "Prefix" + prefix);
                }

                if (dotIndex > -1)
                {
                    _clauseConditions.Add(currentCondition[(dotIndex + 1)..]);
                    currentCondition = currentCondition[..dotIndex];
                }
            }

            _conditions.Add(currentCondition);
        }
    }

    public string GetIdentifier() => _identifier;

    public MagicProperty GetMagicProperty() => _magicProperty;

    public Dictionary<int, string>? GetReferences(Definition definition)
    {
        return _references.TryGetValue(definition.GetTargetName(), out var reference) ? reference : null;
    }

    public bool IsReplicated() => _isReplicated;

    public bool IsDependencyReplicated() => _isDependencyReplicated;

    public bool Matches(Definition candidate)
    {
        var name = candidate.GetTargetName();

        foreach (var subclauses in _clauses)
        {
            var matches = false;
            foreach (var clause in subclauses)
            {
                var negated = clause.StartsWith("!", StringComparison.Ordinal);
                var clauseValue = negated ? clause[1..] : clause;

                var tester = new Regex(Regexify(clauseValue));
                var matcher = tester.Match(name);
                matches = matcher.Success ^ negated;

                if (matches)
                {
                    if (!negated && matcher.Groups.Count > 1)
                    {
                        if (!_references.ContainsKey(name))
                        {
                            _references[name] = new Dictionary<int, string>();
                        }

                        var references = _references[name];
                        for (var i = 1; i < matcher.Groups.Count; ++i)
                        {
                            if (!references.ContainsKey(i))
                            {
                                references[i] = matcher.Groups[i].Value;
                            }
                        }
                    }

                    break;
                }
            }

            if (!matches)
            {
                return false;
            }
        }

        return CheckConditions(_conditions, candidate);
    }

    public bool MatchesClause(Definition clause)
    {
        return CheckConditions(_clauseConditions, clause);
    }

    private bool CheckConditions(List<string> conditions, Definition candidate)
    {
        foreach (var condition in conditions)
        {
            var pieces = condition.Split(':', 2);
            var property = pieces[0];
            var valueSet = pieces.Length == 2 ? pieces[1] : null;
            var isNegated = property.StartsWith("!", StringComparison.Ordinal);

            if (isNegated)
            {
                property = property[1..];
            }

            if (!candidate.HasProperty(property))
            {
                if (!isNegated)
                {
                    return false;
                }
            }
            else if (valueSet != null)
            {
                var matches = false;
                var propertyValue = candidate.GetProperty(property);

                if (valueSet.Length > 2 && valueSet.StartsWith("/") && valueSet.EndsWith("/"))
                {
                    matches = Regex.IsMatch(propertyValue, valueSet[1..^1]);
                }
                else
                {
                    var values = new HashSet<string>(valueSet.Split('|'), StringComparer.OrdinalIgnoreCase);
                    foreach (var value in values)
                    {
                        if (string.Equals(value, propertyValue, StringComparison.OrdinalIgnoreCase))
                        {
                            matches = true;
                            break;
                        }
                    }
                }

                if (matches == isNegated)
                {
                    return false;
                }
            }
            else if (isNegated)
            {
                return false;
            }
        }

        return true;
    }

    private string Regexify(string simplePattern)
    {
        var regexifiedPattern = simplePattern
            .Replace("@*", "([A-Za-z]+)")
            .Replace("#*", "([0-9]+)")
            .Replace("_*", "([A-Za-z0-9]+)")
            .Replace("*", "([A-Za-z0-9]+)");

        var matcher = _grouping.Matches(regexifiedPattern);
        foreach (Match match in matcher)
        {
            var group = match.Value;
            var replacement = "([{0}]{{1}})";
            if (group.StartsWith("@"))
            {
                replacement = string.Format(replacement, "A-Za-z", group.Length);
            }
            else if (group.StartsWith("#"))
            {
                replacement = string.Format(replacement, "0-9", group.Length);
            }
            else
            {
                replacement = string.Format(replacement, "A-Za-z0-9", group.Length);
            }

            regexifiedPattern = Regex.Replace(regexifiedPattern, Regex.Escape(group), replacement, RegexOptions.None, TimeSpan.FromSeconds(1));
        }

        return regexifiedPattern;
    }

    public override bool Equals(object? obj)
    {
        return obj is MagicVariable other && _identifier == other._identifier && _clauses.SequenceEqual(other._clauses) && _conditions.SequenceEqual(other._conditions);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_identifier, _clauses.Count, _conditions.Count);
    }

    public override string ToString()
    {
        return $"MagicVariable {_identifier} [Clauses: {string.Join(";", _clauses.Select(c => string.Join(",", c)))}, Conditions: {string.Join(",", _conditions)}]";
    }
}
