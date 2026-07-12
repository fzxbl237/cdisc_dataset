using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Core.Util;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class UniqueValueValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = { "Variable" };

    private readonly Dictionary<DataEntry, DataEntry> _interns = new();
    private readonly Dictionary<DataGrouping, DataGrouping> _groupings = new();
    private readonly bool _matching;
    private readonly string _variable;
    private readonly string[] _groups;

    public UniqueValueValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        _matching = definition.GetProperty("Matching").Equals("Yes", StringComparison.OrdinalIgnoreCase);

        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }

        _variable = definition.GetProperty("Variable").ToUpperInvariant();
        AddVariable(_variable);

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
        }
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        if (!CheckExpression(dataRecord))
        {
            return 0;
        }

        var count = _groups.Length;
        var group = new DataEntry[Math.Max(count, 1)];
        var value = Intern(dataRecord.GetValue(_variable));

        if (count != 0)
        {
            for (var i = 0; i < count; ++i)
            {
                group[i] = Intern(dataRecord.GetValue(_groups[i]));
            }
        }
        else
        {
            group[0] = value;
        }

        var search = new DataGrouping(group);
        if (!_groupings.TryGetValue(search, out var result) && (!_matching || count > 0 || _groupings.Count == 0))
        {
            result = _matching ? new MatchingDataGrouping(search) : new UniqueDataGrouping(search);
            _groupings[result] = result;
        }

        return (byte)(result != null && result.Accepts(value) ? 2 : 1);
    }

    protected override List<Outcome> PerformDatasetValidation(SourceDetails entity)
    {
        _interns.Clear();
        _groupings.Clear();
        return base.PerformDatasetValidation(entity);
    }

    private DataEntry Intern(DataEntry entry)
    {
        if (_interns.TryGetValue(entry, out var existing))
        {
            return existing;
        }

        _interns[entry] = entry;
        return entry;
    }

    private sealed class UniqueDataGrouping : DataGrouping
    {
        private readonly HashSet<DataEntry> _values = new();

        public UniqueDataGrouping(DataGrouping grouping)
            : base(grouping)
        {
        }

        public override bool Accepts(DataEntry entry) => _values.Add(entry);
    }

    private sealed class MatchingDataGrouping : DataGrouping
    {
        private DataEntry? _value;

        public MatchingDataGrouping(DataGrouping grouping)
            : base(grouping)
        {
        }

        public override bool Accepts(DataEntry entry)
        {
            if (_value == null)
            {
                _value = entry;
                return true;
            }

            return _value.Equals(entry);
        }
    }
}
