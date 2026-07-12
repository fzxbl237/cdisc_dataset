using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class DomainPropertyValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = ["Properties", "Test"] ;

    private readonly List<SourceDetails.Property> _properties = new();

    public DomainPropertyValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Dataset, RequiredVariables, metrics)
    {
        PrepareExpression(definition.GetProperty("Test"), false);

        foreach (var property in definition.GetProperty("Properties").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<SourceDetails.Property>(property.Trim(), out var parsed))
            {
                _properties.Add(parsed);
            }
        }
    }

    protected override List<Outcome> PerformDatasetValidation(SourceDetails entity)
    {
        var results = new List<Outcome>();
        var entities = entity.HasProperty(SourceDetails.Property.Combined)
            ? entity.GetSplitSources()
            : new List<SourceDetails> { entity };

        foreach (var dataset in entities)
        {
            var values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase);
            var count = 1;

            foreach (var property in _properties)
            {
                values["P" + count] = dataset.HasProperty(property)
                    ? Session.GetDataEntryFactory().Create(dataset.GetString(property))
                    : DataEntry.NullEntry;
                count++;
            }

            var record = new DataRecordImpl(null!, null!, values);
            var result = CheckExpression(record);
            var outcome = new Outcome((byte)(result ? 2 : 1), dataset);

            if (!result)
            {
                count = 1;
                foreach (var property in _properties)
                {
                    outcome.Display[property.ToString()] = record.GetValue("P" + count).ToString();
                    count++;
                }
            }

            results.Add(outcome);
        }

        return results;
    }
}
