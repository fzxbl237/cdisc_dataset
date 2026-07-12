using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Rules.Expressions;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public abstract class AbstractScriptableValidationRule : AbstractValidationRule
{
    protected const string DefaultExpressionName = "DEFAULT_EXPRESSION";

    private readonly Dictionary<string, DataEntry> _nullableValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Expression> _expressions = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasExpressions;

    protected AbstractScriptableValidationRule(RuleDefinition definition, ValidationSession token, ValidationRule.Target target, string[] required,
        WritableRuleMetrics.Scope metrics)
        : base(definition, token, target, required, metrics)
    {
        if (definition.HasProperty("Optional"))
        {
            var nullableVariables = definition.GetProperty("Optional").Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var nullableVariable in nullableVariables)
            {
                _nullableValues[nullableVariable.Trim().ToUpperInvariant()] = DataEntry.NullEntry;
            }
        }
    }

    public override  bool Validate(DataRecord dataRecord, Action<Diagnostic>? reporter)
    {
        if (_nullableValues.Count > 0)
        {
            dataRecord = new SupplementalDataRecord(dataRecord, _nullableValues, true);
        }

        return base.Validate(dataRecord, reporter);
    }

    protected new void AddVariable(string variable)
    {
        var normalized = variable.ToUpperInvariant();
        if (!_nullableValues.ContainsKey(normalized))
        {
            base.AddVariable(normalized);
        }
    }

    protected bool CheckExpression(DataRecord dataRecord)
    {
        return CheckExpression(dataRecord, DefaultExpressionName);
    }

    protected bool CheckExpression(DataRecord dataRecord, string name)
    {
        if (!_expressions.TryGetValue(name, out var expression))
        {
            return true;
        }

        try
        {
            return expression.Evaluate(dataRecord);
        }
        catch (EvaluationException ex)
        {
            throw new CorruptRuleException(CorruptRuleException.State.Temporary, GetID(), ex.Message, ex.Description);
        }
    }

    protected bool HasExpression()
    {
        return HasExpression(DefaultExpressionName);
    }

    protected bool HasExpression(string name)
    {
        return _hasExpressions && _expressions.ContainsKey(name);
    }

    protected void PrepareExpression(string condition)
    {
        PrepareExpression(DefaultExpressionName, condition);
    }

    protected void PrepareExpression(string condition, bool registerVariables)
    {
        PrepareExpression(DefaultExpressionName, condition, registerVariables);
    }

    protected void PrepareExpression(string name, string testCondition)
    {
        PrepareExpression(name, testCondition, true);
    }

    protected void PrepareExpression(string name, string testCondition, bool registerVariables)
    {
        if (testCondition == null)
        {
            throw new ArgumentException("testCondition cannot be null", nameof(testCondition));
        }

        var compiled = Expression.CreateFrom(testCondition, Session.GetOptions(), Session.GetDataEntryFactory());
        if (registerVariables)
        {
            foreach (var variable in compiled.GetVariables())
            {
                AddVariable(variable);
            }
        }

        _hasExpressions = true;
        _expressions[name] = compiled;
    }

    protected new IDictionary<string, string> PullRecordValues(DataRecord record, HashSet<string> variables)
    {
        var combined = new HashSet<string>(variables, StringComparer.OrdinalIgnoreCase);
        foreach (var key in _nullableValues.Keys)
        {
            combined.Add(key);
        }

        return base.PullRecordValues(record, combined);
    }
}
