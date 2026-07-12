using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class ConditionalValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = ["Test"];

    public ConditionalValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }

        PrepareExpression("TEST", definition.GetProperty("Test"));
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        if (!CheckExpression(dataRecord))
        {
            return 0;
        }

        return (byte)(CheckExpression(dataRecord, "TEST") ? 2 : 1);
    }
}
