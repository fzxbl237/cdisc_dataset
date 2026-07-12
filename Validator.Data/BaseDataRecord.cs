namespace P21.Validator.Data;

public abstract class BaseDataRecord : DataRecord
{
    protected readonly IReadOnlyDictionary<string, DataEntry> Values;

    protected BaseDataRecord(IReadOnlyDictionary<string, DataEntry> values)
    {
        Values = new Dictionary<string, DataEntry>(values, StringComparer.OrdinalIgnoreCase);
    }

    public virtual bool DefinesVariable(string variable)
    {
        return Values.ContainsKey(variable);
    }

    public virtual DataEntry GetValue(string variable)
    {
        if (!Values.TryGetValue(variable, out var value))
        {
            throw new ArgumentException($"The variable {variable} does not exist in this DataRecord");
        }

        return value;
    }

    public virtual IReadOnlyCollection<string> GetVariables() => Values.Keys.ToList();

    public abstract bool IsTransient(string variable);

    public abstract Api.Models.DataDetails GetDataDetails();

    public abstract Api.Models.SourceDetails GetSourceDetails();

    public abstract int GetId();
}
