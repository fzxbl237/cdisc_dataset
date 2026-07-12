using System.Text.RegularExpressions;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class MatchValidationRule : AbstractScriptableValidationRule
{

    private static readonly string DefaultDelimiter = ",";
    private static readonly string DefaultPairDelimiter = ":";
    private static readonly string[] RequiredVariables = ["Variable","Terms"];

    private readonly string variable;
    private readonly string pairedVariable;
    private readonly Dictionary<string, string> accpetableValues = new();
    private bool isCaseSensitive = true;
    

    //private readonly IReadOnlyList<string> _matchTargets;

    public MatchValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        this.variable = definition.GetProperty("Variable").ToUpper();
        string delimiter = DefaultDelimiter;
        string pairDelimiter = DefaultPairDelimiter;
        
        AddVariable(variable);

        if (definition.HasProperty("PairedVariable"))
        {
            this.pairedVariable = definition.GetProperty("PairedVariable").ToUpper();
            AddVariable(pairedVariable);
        }
        else
        {
            //TODO: may be null
            pairedVariable = string.Empty;
        }

        if (definition.HasProperty("Delimiter"))
        {
            delimiter = definition.GetProperty("Delimiter");
        }

        if (definition.HasProperty("PairDelimiter"))
        {
            pairDelimiter = definition.GetProperty("PairDelimiter");
        }

        if (definition.HasProperty("CaseSensitive"))
        {
            if (definition.GetProperty("CaseSensitive").Equals("No", StringComparison.InvariantCultureIgnoreCase))
            {
                isCaseSensitive = false;
            }
        }
        
        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }
        string[] matchValues = definition.GetProperty("Terms").Split(Regex.Escape(delimiter));
        
        foreach (var value in matchValues)
        {
            var matchValue = value;
            if (!isCaseSensitive)
            {
                matchValue = value.ToUpper();
            }

            var paired = matchValue;

            if (!string.IsNullOrWhiteSpace(pairedVariable))
            {
                var pairs = matchValue.Split(Regex.Escape(pairDelimiter));
                matchValue = pairs[0];
                paired = pairs[1];
            }
            accpetableValues.Add(matchValue.Trim(),paired.Trim());
        }
    }

    protected override byte PerformValidation(DataRecord record)
    {
        if (!CheckExpression(record))
        {
            return 0;
        }

        bool result = true;

        var entry = record.GetValue(variable);
        if (entry.HasValue)
        {
            var value = entry.ToString()??string.Empty;
            if (!isCaseSensitive)
            {
                value = value?.ToUpper();
            }

            if (string.IsNullOrWhiteSpace(pairedVariable) && !string.IsNullOrWhiteSpace(value))
            {
                result = accpetableValues.ContainsKey(value);
            }
            else
            {
                accpetableValues.TryGetValue(value, out string? expected);
                entry = record.GetValue(pairedVariable);
                if (entry.HasValue)
                {
                    value = entry.ToString()??string.Empty;
                    if (!isCaseSensitive)
                    {
                        value = value?.ToUpper();
                    }
                    
                    if (accpetableValues.Values.Contains(value) || string.IsNullOrWhiteSpace(expected))
                    {
                        result = value.Equals(expected);
                    }
                }
            }
        }

        

        return (byte)(result ? 2 : 1);
    }
}
