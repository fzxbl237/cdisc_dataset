namespace P21.Validator.Data;

public sealed class SubjectDataSupplement : DataSupplement
{
    public const string SubjectVariablePrefix = "SUB:";

    private const string KeyVariable = "USUBJID";
    private const string AltKeyVariable = "POOLID";

    private readonly HashSet<string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<DataEntry, DataEntry> _interns = new();
    private readonly Dictionary<DataEntry, DataEntry[]> _groupings = new();

    private readonly string? _keyVariable;

    public SubjectDataSupplement(DataSource originalSource)
    {
        using var source = originalSource.Replicate();
        var variables = new HashSet<string>(source.GetVariables(), StringComparer.OrdinalIgnoreCase);

        if (variables.Contains(KeyVariable))
        {
            _keyVariable = KeyVariable;
        }
        else if (variables.Contains(AltKeyVariable))
        {
            _keyVariable = AltKeyVariable;
        }
        else
        {
            _keyVariable = null;
            return;
        }

        var count = variables.Count;
        while (source.HasRecords())
        {
            foreach (var record in source.GetRecords())
            {
                var key = Intern(record.GetValue(_keyVariable));
                var values = new DataEntry[count];
                var i = 0;
                foreach (var variable in variables)
                {
                    values[i++] = Intern(record.GetValue(variable));
                }

                _groupings[key] = values;
            }
        }

        foreach (var variable in variables)
        {
            _variables.Add(SubjectVariablePrefix + variable);
        }
    }

    public DataRecord Augment(DataRecord record)
    {
        if (_keyVariable != null && record.DefinesVariable(_keyVariable))
        {
            var key = record.GetValue(_keyVariable);
            _groupings.TryGetValue(key, out var values);

            var supplementalValues = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase);
            var count = 0;

            foreach (var variable in _variables)
            {
                supplementalValues[variable] = values != null ? values[count] : DataEntry.NullEntry;
                count++;
            }

            if (count > 0)
            {
                return new SupplementalDataRecord(record, supplementalValues, false);
            }
        }

        return record;
    }

    private DataEntry Intern(DataEntry entry)
    {
        if (_interns.TryGetValue(entry, out var existing))
        {
            return existing;
        }

        _interns[entry] = entry;
        return entry;
    }
}
