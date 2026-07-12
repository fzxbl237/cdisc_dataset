using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class VariableLengthValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = ["Variable"];

    private readonly int _min;
    private readonly int _max;
    private readonly Dictionary<string, int> _lengths = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _countExcess;

    public VariableLengthValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Dataset, RequiredVariables, metrics)
    {
        foreach (var variable in definition.GetContexts())
        {
            _lengths[variable.GetTargetName().ToUpperInvariant()] = 0;
        }

        if (!definition.HasProperty("Minimum") && !definition.HasProperty("Maximum"))
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                "VariableLength rules must have one of (Minimum, Maximum)");
        }

        var min = 0;
        var max = -1;

        if (definition.HasProperty("Minimum") && !int.TryParse(definition.GetProperty("Minimum"), out min))
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                "Invalid value for Minimum, expected number");
        }

        if (definition.HasProperty("Maximum") && !int.TryParse(definition.GetProperty("Maximum"), out max))
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                "Invalid value for Maximum, expected number");
        }

        _min = Math.Max(0, min);
        _max = max;
        _countExcess = !definition.GetProperty("Count").Equals("Length", StringComparison.OrdinalIgnoreCase);
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        foreach (var variable in _lengths.Keys)
        {
            if (!dataRecord.DefinesVariable(variable))
            {
                continue;
            }

            var entry = dataRecord.GetValue(variable);
            if (entry.HasValue)
            {
                var length = entry.ToString().Length;
                if (length > _lengths[variable])
                {
                    _lengths[variable] = length;
                }
            }
        }

        return 0;
    }

    protected override List<Outcome> PerformDatasetValidation(SourceDetails entity)
    {
        var entities = entity.HasProperty(SourceDetails.Property.Combined)
            ? entity.GetSplitSources()
            : new List<SourceDetails> { entity };
        var maximums = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataset in entities)
        {
            foreach (var name in _lengths.Keys)
            {
                var variable = dataset.GetVariable(name);
                if (variable != null && variable.HasProperty(VariableDetails.Property.Length))
                {
                    var length = variable.GetInteger(VariableDetails.Property.Length);
                    if (!maximums.TryGetValue(name, out var current) || current < length)
                    {
                        maximums[name] = length;
                    }
                }
            }
        }

        var results = new List<Outcome>();

        foreach (var variable in _lengths.Keys)
        {
            maximums.TryGetValue(variable, out var max);
            Outcome result;

            if (max != 0 || !_countExcess)
            {
                var length = _lengths[variable];
                bool failed;

                if (_countExcess)
                {
                    if (length > 0)
                    {
                        length = max - length;
                        failed = length >= _min && (_max == -1 || length <= _max);
                    }
                    else
                    {
                        failed = false;
                    }
                }
                else
                {
                    failed = length < _min || (_max != -1 && length > _max);
                }

                result = new Outcome((byte)(!failed ? 2 : 1));
                if (failed)
                {
                    result.Display["Variable"] = variable;
                    result.Display[_countExcess ? "Excess" : "Length"] = length.ToString();
                }
            }
            else
            {
                result = new Outcome((byte)0);
            }

            results.Add(result);
        }

        return results;
    }
}
