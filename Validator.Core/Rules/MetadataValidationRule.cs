using P21.Validator.Core.Report;
using P21.Validator.Core.Rules.Expressions;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class MetadataValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = { "From" };

    private readonly bool _ignoreFromFailure;
    private readonly LookupProvider _provider;
    private readonly PreparedQuery _query;

    public MetadataValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }

        _ignoreFromFailure = definition.HasProperty("Variable") && definition.GetProperty("Variable").Length > 0;
        _provider = Session.GetLookupProvider(null);
        var target = definition.HasProperty("From") ? definition.GetProperty("From") : null;
        var search = definition.HasProperty("Variable") ? definition.GetProperty("Variable") : null;
        _query = new PreparedQuery(target, search);

        foreach (var variable in _query.GetLocal())
        {
            AddVariable(variable);
        }
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        if (!CheckExpression(dataRecord))
        {
            return 0;
        }

        var target = _query.GetTarget(dataRecord);
        bool result;

        if (_provider.VerifyExists(target))
        {
            var remote = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in _query.GetSearch(dataRecord))
            {
                remote.Add(mapping.GetRemote());
            }

            result = _provider.VerifyExists(target, remote);
        }
        else
        {
            result = _ignoreFromFailure;
        }

        return (byte)(result ? 2 : 1);
    }
}
