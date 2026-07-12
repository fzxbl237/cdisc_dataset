using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class FindValidationRule : AbstractScriptableValidationRule
{
    private const string DefaultDelimiter = ",";
    private static readonly string[] RequiredVariables = { "Variable" };

    private readonly HashSet<string> _terms = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _isCaseSensitive = true;
    private readonly int _matchCount;
    private readonly int _ifStatements;
    private readonly bool _matchExact;
    private readonly string _variable;
    private readonly string _value;
    private readonly Regex? _test;
    private readonly string[] _groups;
    private readonly Dictionary<DataGrouping, DataGrouping> _groupings = new();
    private int _counter;

    public FindValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Dataset, RequiredVariables, metrics)
    {
        _variable = definition.GetProperty("Variable").ToUpperInvariant();
        AddVariable(_variable);

        if (definition.HasProperty("Terms") == definition.HasProperty("Test"))
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                "Find rules must have one of (Terms, Test), and cannot have both.");
        }

        var storeValue = string.Empty;
        var delimiter = DefaultDelimiter;
        var matchCount = -1;

        if (definition.HasProperty("Match"))
        {
            var value = definition.GetProperty("Match");
            if (value.Equals("One", StringComparison.OrdinalIgnoreCase))
            {
                matchCount = 1;
            }
            else if (!int.TryParse(value, out matchCount))
            {
                throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                    "Invalid value for Match attribute, expected a number or 'one'");
            }
        }

        _matchExact = definition.GetProperty("MatchExact").Equals("Yes", StringComparison.OrdinalIgnoreCase);

        bool isCaseSensitive = !(definition.HasProperty("CaseSensitive") && definition.GetProperty("CaseSensitive").Equals("No", StringComparison.OrdinalIgnoreCase));

        _isCaseSensitive = isCaseSensitive;

        if (definition.HasProperty("Terms"))
        {
            storeValue = definition.GetProperty("Terms");
            if (definition.HasProperty("Delimiter"))
            {
                delimiter = definition.GetProperty("Delimiter");
            }

            var matchValues = definition.GetProperty("Terms").Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
            foreach (var value in matchValues)
            {
                var trimmed = value.Trim();
                if (!_isCaseSensitive)
                {
                    trimmed = trimmed.ToUpperInvariant();
                }

                _terms.Add(trimmed);
            }

            _counter = _terms.Count;
            if (matchCount < 0)
            {
                matchCount = _counter;
            }

            _test = null;
        }
        else
        {
            var options = _isCaseSensitive ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase;
            _test = new Regex(definition.GetProperty("Test"), options);
            if (matchCount < 0)
            {
                matchCount = 1;
            }
        }

        if (definition.HasProperty("If"))
        {
            var tests = definition.GetProperty("If").Split("@iand", StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tests.Length; ++i)
            {
                PrepareExpression("IF" + i, tests[i]);
            }

            _ifStatements = tests.Length;
        }
        else
        {
            _ifStatements = 0;
        }

        if (definition.HasProperty("GroupBy"))
        {
            var groups = definition.GetProperty("GroupBy").Split(',', StringSplitOptions.RemoveEmptyEntries);
            _groups = new string[groups.Length];

            for (var i = 0; i < groups.Length; ++i)
            {
                _groups[i] = groups[i].Trim().ToUpperInvariant();
                AddVariable(_groups[i]);
            }
        }
        else
        {
            _groups = Array.Empty<string>();
            var group = new DataGrouping(Array.Empty<DataEntry>(), _counter, _ifStatements, _terms);
            _groupings[group] = group;
        }

        _matchCount = matchCount;

        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }

        _value = storeValue;
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        var group = new DataEntry[_groups.Length];
        for (var i = 0; i < _groups.Length; ++i)
        {
            group[i] = dataRecord.GetValue(_groups[i]);
        }

        var search = new DataGrouping(group, _counter, _ifStatements, _terms);
        if (!_groupings.TryGetValue(search, out var result))
        {
            _groupings[search] = search;
            result = search;
        }

        if (HasExpression("IF0") && !result.IsActivated())
        {
            for (var i = 0; i < _ifStatements; ++i)
            {
                if (!result.IsActivated(i) && CheckExpression(dataRecord, "IF" + i))
                {
                    result.Activate(i);
                }
            }
        }

        var entry = dataRecord.GetValue(_variable);
        if (CheckExpression(dataRecord) && entry.HasValue)
        {
            var value = entry.ToString();
            if (_test == null)
            {
                if (!_isCaseSensitive)
                {
                    value = value.ToUpperInvariant();
                }

                result.Terms.Remove(value);
            }
            else if (_test.IsMatch(value))
            {
                result.Counter++;
            }
        }

        return 0;
    }

    protected override List<Outcome> PerformDatasetValidation(SourceDetails entity)
    {
        var results = new List<Outcome>();

        foreach (var grouping in _groupings.Keys)
        {
            if (!HasExpression("IF0") || grouping.IsActivated())
            {
                var result = true;
                if (_test == null)
                {
                    if (!_matchExact)
                    {
                        if (grouping.Counter - _matchCount < grouping.Terms.Count)
                        {
                            result = false;
                        }
                    }
                    else if (grouping.Counter - _matchCount != grouping.Terms.Count)
                    {
                        result = false;
                    }
                }
                else
                {
                    if (!_matchExact)
                    {
                        if (grouping.Counter < _matchCount)
                        {
                            result = false;
                        }
                    }
                    else if (grouping.Counter != _matchCount)
                    {
                        result = false;
                    }
                }

                var outcome = new Outcome((byte)(result ? 2 : 1));
                outcome.Display[_variable] = _value;
                for (var i = 0; i < _groups.Length; ++i)
                {
                    outcome.Display[_groups[i]] = grouping.Group[i].ToString();
                }

                results.Add(outcome);
            }
        }

        return results.Count == 0 ? base.PerformDatasetValidation(entity) : results;
    }

    private sealed class DataGrouping : IEquatable<DataGrouping>
    {
        public DataGrouping(DataEntry[] group, int counter, int conditions, HashSet<string> terms)
        {
            Group = group;
            Counter = counter;
            Terms = new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);
            _activated = new bool[conditions];
        }

        private readonly bool[] _activated;
        private bool _isActivated;

        public DataEntry[] Group { get; }
        public HashSet<string> Terms { get; }
        public int Counter { get; set; }

        public void Activate(int i)
        {
            _activated[i] = true;
            _isActivated = _activated.All(active => active);
        }

        public bool IsActivated() => _isActivated;

        public bool IsActivated(int i) => _activated[i];

        public bool Equals(DataGrouping? other)
        {
            if (other == null)
            {
                return false;
            }

            return Group.SequenceEqual(other.Group);
        }

        public override bool Equals(object? obj) => Equals(obj as DataGrouping);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var entry in Group)
            {
                hash.Add(entry);
            }

            return hash.ToHashCode();
        }
    }
}
