namespace P21.Validator.Data;

public sealed class GlobalDataSupplement : DataSupplement
{
    public const string GlobalVariablePrefix = "VAR:";

    private const string KeyVariable = "PARMCD";
    private const string ValueVariable = "VAL";

    private readonly IReadOnlyDictionary<string, DataEntry> _supplementalValues;

    public GlobalDataSupplement(DataSource originalSource)
    {
        using var source = originalSource.Replicate();
        var variables = source.GetVariables();
        var prefix = source.GetName();
        var values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase);

        if (variables.Contains(prefix + KeyVariable) && variables.Contains(prefix + ValueVariable))
        {
            while (source.HasRecords())
            {
                foreach (var record in source.GetRecords())
                {
                    var variable = GlobalVariablePrefix + System.Text.RegularExpressions.Regex.Replace(
                        record.GetValue(prefix + KeyVariable).ToString().ToUpperInvariant(),
                        "[^A-Z]+",
                        string.Empty
                    );

                    if (!values.ContainsKey(variable))
                    {
                        values[variable] = record.GetValue(prefix + ValueVariable);
                    }
                }
            }
        }

        _supplementalValues = values;
    }

    public DataRecord Augment(DataRecord record)
    {
        return new SupplementalDataRecord(record, _supplementalValues, false);
    }
}
