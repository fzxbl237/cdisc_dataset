using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Core.Util;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class VariableOrderValidationRule : AbstractValidationRule
{
    private static readonly string[] RequiredVariables = { "Variable" };

    private readonly Dictionary<string, int> _ordering = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _placed = new(StringComparer.OrdinalIgnoreCase);

    public VariableOrderValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        AddVariable("VARIABLE");
        AddVariable("DATASET");

        foreach (var variable in definition.GetContexts())
        {
            var name = variable.GetTargetName().ToUpperInvariant();
            if (!int.TryParse(variable.GetProperty("Config.OrderNumber"), out var order))
            {
                throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                    string.Format("Invalid value for Order attribute of variable {0}", name));
            }

            _ordering[name] = order;
        }
    }

    public override void Setup(SourceDetails entity)
    {
        var orders = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var namesets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var entities = entity.HasProperty(SourceDetails.Property.Combined)
            ? entity.GetSplitSources()
            : new List<SourceDetails> { entity };

        foreach (var dataset in entities)
        {
            var parent = dataset.GetParent();
            var datasetName = parent.GetString(SourceDetails.Property.Subname);

            if (!_placed.TryGetValue(datasetName, out var placed))
            {
                placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _placed[datasetName] = placed;
                namesets[datasetName] = new List<string>();
                orders[datasetName] = new List<int>();
            }

            var names = namesets[datasetName];
            var order = orders[datasetName];

            foreach (var variable in parent.GetVariables())
            {
                var variableName = variable.GetString(VariableDetails.Property.Name);
                if (_ordering.TryGetValue(variableName, out var expected))
                {
                    names.Add(variableName);
                    order.Add(expected);
                }
                else
                {
                    placed.Add(variableName);
                }
            }
        }

        foreach (var dataset in _placed.Keys)
        {
            var placed = _placed[dataset];
            var names = namesets[dataset];
            var order = orders[dataset];

            var ordered = order.ToArray();
            var indexes = Helpers.DetermineLargestIncreasingSubsequence(ordered, true);
            foreach (var index in indexes)
            {
                placed.Add(names[index]);
            }
        }
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        var dataset = dataRecord.GetValue("DATASET").ToString().ToUpperInvariant();
        if (!_placed.TryGetValue(dataset, out var placed))
        {
            return 1;
        }

        var variable = dataRecord.GetValue("VARIABLE").ToString().ToUpperInvariant();
        return (byte)(placed.Contains(variable) ? 1 : 0);
    }
}
