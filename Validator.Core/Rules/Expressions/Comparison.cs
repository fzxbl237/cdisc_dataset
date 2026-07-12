using System.Numerics;
using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Api.Options;
using P21.Validator.Data;
using P21.Validator.Core.Rules.Expressions.Functions;
using P21.Validator.Core.Settings;

namespace P21.Validator.Core.Rules.Expressions;

internal sealed class Comparison : Evaluable
{
    private const double DefaultEpsilon = 0.001;
    private const string VariablePattern = "[A-Za-z][A-Za-z0-9]*|(?:VAL|SUB|VAR):[A-Za-z0-9]+";

    private readonly string _lhs;
    private readonly string? _rhs;
    private readonly DataEntry? _constant;
    private readonly string _operator;
    private readonly Function? _function;
    private readonly bool _equal;
    private readonly bool _greater;
    private readonly bool _less;
    private readonly double _epsilon;

    public static Evaluable CreateComparison(string expression, ValidationOptions options, DataEntryFactory factory)
    {
        expression = expression.Trim();

        var pattern = new Regex(
            "(" + VariablePattern + ")" +
            "(?:\\s*([<>]|[%.^=<>!~]=)\\s*)" +
            "((?:" + VariablePattern + ")|'(?:[^']|\\\\')+'|(?:-?[0-9]+(?:\\.[0-9]+)?)|null|:[A-Z]+\\([^)]+\\))");

        var matcher = pattern.Match(expression);
        if (!matcher.Success || matcher.Groups.Count != 4)
        {
            throw new SyntaxException($"The comparison clause {expression} not properly formatted");
        }

        var lhs = matcher.Groups[1].Value;
        var rhs = matcher.Groups[3].Value.Replace("\\'", "'");
        var op = matcher.Groups[2].Value;

        if (rhs == "null")
        {
            var isNegated = op == "!=";
            if (!isNegated && op != "==")
            {
                throw new SyntaxException($"The comparison clause {expression} attempts to compare the variable {lhs} to null via an incompatible operator ({op})");
            }

            return new NullComparison(lhs, isNegated);
        }

        if (op == "~=")
        {
            return new RegexComparison(lhs, rhs[1..^1]);
        }

        var epsilon = DefaultEpsilon;
        if (options.HasProperty("Engine.FuzzyTolerance") && double.TryParse(options.GetProperty("Engine.FuzzyTolerance"), out var parsed))
        {
            epsilon = parsed;
        }

        return new Comparison(lhs, rhs, op, epsilon, factory);
    }

    private Comparison(string lhs, string rhs, string op, double epsilon, DataEntryFactory factory)
    {
        _lhs = lhs;
        _operator = op;
        _equal = op is "<=" or ">=" or "==";
        _greater = op is ">=" or ">" or "!=";
        _less = op is "<=" or "<" or "!=";
        _epsilon = epsilon;

        if (Regex.IsMatch(rhs, "^" + VariablePattern + "$") )
        {
            _rhs = rhs;
            _constant = null;
            _function = null;
        }
        else if (rhs.StartsWith(":", StringComparison.Ordinal))
        {
            _rhs = null;
            _constant = null;
            _function = Create.CreateFunction(rhs, factory);
        }
        else
        {
            _rhs = null;
            if (rhs.StartsWith("'"))
            {
                rhs = rhs[1..^1];
            }

            _constant = factory.Create(rhs);
            _function = null;
        }
    }

    public bool Evaluate(DataRecord record)
    {
        var lhs = record.GetValue(_lhs);
        DataEntry rhs;

        if (_constant == null)
        {
            rhs = _function == null ? record.GetValue(_rhs!) : _function.Compute(record);
        }
        else
        {
            rhs = _constant;
        }

        switch (_operator)
        {
            case "^=":
            case ".=":
                var comparison = lhs.CompareToAny(rhs, false);
                return _operator == "^=" ? comparison == 0 : comparison != 0;
            case "%=":
                if (lhs.IsNumeric && rhs.IsNumeric)
                {
                    var lhsValue = (decimal)lhs.GetValue();
                    var rhsValue = (decimal)rhs.GetValue();
                    return NearlyEqual(lhsValue, rhsValue, _epsilon);
                }

                return lhs.CompareToAny(rhs, true) == 0;
            default:
                var cmp = lhs.CompareToAny(rhs, true);
                if (cmp == 0)
                {
                    return _equal;
                }

                return cmp > 0 ? _greater : _less;
        }
    }

    public HashSet<string> GetVariables()
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _lhs };
        if (_rhs != null)
        {
            variables.Add(_rhs);
        }
        else if (_function != null)
        {
            foreach (var variable in _function.GetVariables())
            {
                variables.Add(variable);
            }
        }

        return variables;
    }

    public override string ToString()
    {
        var rhs = _rhs;
        if (_constant != null)
        {
            rhs = "'" + _constant + "'";
        }
        else if (_function != null)
        {
            rhs = _function.ToString();
        }

        return $"{_lhs} {_operator} {rhs}";
    }

    private sealed class RegexComparison : Evaluable
    {
        private readonly string _lhs;
        private readonly Regex _pattern;

        public RegexComparison(string lhs, string rhs)
        {
            _lhs = lhs;
            _pattern = new Regex(rhs);
        }

        public bool Evaluate(DataRecord record)
        {
            var value = record.GetValue(_lhs).ToString();
            return _pattern.IsMatch(value);
        }

        public HashSet<string> GetVariables() => new(StringComparer.OrdinalIgnoreCase) { _lhs };

        public override string ToString() => $"{_lhs} ~= '{_pattern}'";
    }

    private sealed class NullComparison : Evaluable
    {
        private readonly string _lhs;
        private readonly bool _isNegated;

        public NullComparison(string lhs, bool isNegated)
        {
            _lhs = lhs;
            _isNegated = isNegated;
        }

        public bool Evaluate(DataRecord record)
        {
            var entry = record.GetValue(_lhs);
            return _isNegated == entry.HasValue;
        }

        public HashSet<string> GetVariables() => new(StringComparer.OrdinalIgnoreCase) { _lhs };

        public override string ToString() => $"{_lhs} {(_isNegated ? "!=" : "==")} null";
    }

    private static bool NearlyEqual(decimal a, decimal b, double epsilon)
    {
        if (a == b)
        {
            return true;
        }

        var da = (double)a;
        var db = (double)b;
        var diff = Math.Abs(da - db);
        var absA = Math.Abs(da);
        var absB = Math.Abs(db);

        if (da == 0 || db == 0 || diff < float.MinValue)
        {
            return diff < (epsilon * float.MinValue);
        }

        return diff / (absA + absB) < epsilon;
    }
}
