using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;
using System.Text.RegularExpressions;

namespace P21.Validator.Core.Rules;

public sealed class UniqueValueValidator:AbstractScriptableValidationRule
{
    private readonly Dictionary<DataEntry, DataEntry> _interns = new();
    private readonly Dictionary<DataGrouping, DataGrouping> _groupings = new();
    private readonly bool _matching;
    private readonly string _variable;
    private readonly string[] _groups;

    private static readonly string[] REQUIRED_VARIABLES = ["Variable"];

//public UniqueValueValidator(bool matching, string variable, params string[] groupBy)
//{
//    _matching = matching;
//    _variable = variable.ToUpperInvariant();
//    _groups = groupBy.Select(group => group.Trim().ToUpperInvariant()).Where(group => group.Length > 0).ToArray();
//}

public UniqueValueValidator(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics) 
        : base(definition, token, ValidationRule.Target.Record, REQUIRED_VARIABLES, metrics)
    {
        this._matching = definition.GetProperty("Matching").Equals("Yes", StringComparison.CurrentCultureIgnoreCase);
        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }
        this._variable = definition.GetProperty("Variable").ToUpper();
        AddVariable(this._variable);

        if (definition.HasProperty("GroupBy"))
        {
            string[] groups = definition.GetProperty("GroupBy").Split(",");
            this._groups = new string[groups.Length];
            for (int i = 0; i < groups.Length; i++) { 
                _groups[i] = groups[i].Trim().ToUpper();
                AddVariable(_groups[i]);
            }
        }
        else
        {
            _groups = [];
        }

    }

    protected override byte PerformValidation(DataRecord record)
    {
        var res=CheckExpression(record);
        if (!CheckExpression(record)) return 0;
        var count = _groups.Length;
        var group = new DataEntry[Math.Max(count, 1)];
        var value = Intern(record.GetValue(_variable));

        if (count != 0)
        {
            for (var i = 0; i < count; ++i)
            {
                group[i] = Intern(record.GetValue(_groups[i]));
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
        return (byte)((result != null && result.Accepts(value) ? 2 : 1));
    }

    public void Reset()
    {
        _interns.Clear();
        _groupings.Clear();
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

    //private abstract class GroupingBase : DataGrouping
    //{
    //    protected GroupingBase(DataGrouping grouping) : base(grouping.Entries)
    //    {
    //    }

    //    public abstract  bool Accepts(DataEntry entry);
    //}

    private sealed class UniqueDataGrouping : DataGrouping
    {
        private readonly HashSet<DataEntry> _values = new();

        public UniqueDataGrouping(DataGrouping grouping) : base(grouping)
        {
        }

        public override bool Accepts(DataEntry entry)
        {
            return _values.Add(entry);
        }
    }

    private sealed class MatchingDataGrouping : DataGrouping
    {
        private DataEntry? _value;

        public MatchingDataGrouping(DataGrouping grouping) : base(grouping)
        {
        }

        public override bool Accepts(DataEntry entry)
        {
            if (_value is null)
            {
                _value = entry;
                return true;
            }

            return _value.Equals(entry);
        }
    }
}
