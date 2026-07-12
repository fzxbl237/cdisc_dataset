using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class ConditionalRequiredValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = ["Variable"];

    private readonly string _variable;

    public ConditionalRequiredValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }

        _variable = definition.GetProperty("Variable").ToUpper();
        AddVariable(_variable);
    }

    protected override byte PerformValidation(DataRecord record)
    {
        if (!CheckExpression(record))
        {
            return 0;
        }

        var entry = record.GetValue(_variable);
        

        return (byte)(entry.HasValue?2:1);
    }
}
