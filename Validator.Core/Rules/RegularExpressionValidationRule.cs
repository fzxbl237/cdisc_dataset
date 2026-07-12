using System.Text.RegularExpressions;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class RegularExpressionValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = ["Variable", "Test" ];

    private readonly string _variable;
    private readonly Regex _pattern;

    public RegularExpressionValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        _variable = definition.GetProperty("Variable").ToUpperInvariant();
        _pattern = new Regex(definition.GetProperty("Test"), RegexOptions.Compiled);

        AddVariable(_variable);

        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        if (!CheckExpression(dataRecord))
        {
            return 0;
        }

        var value = dataRecord.GetValue(_variable);
        if (!value.HasValue)
        {
            return 0;
        }

        return (byte)(_pattern.IsMatch(value.ToString()??string.Empty) ? 2 : 1);
    }
}
