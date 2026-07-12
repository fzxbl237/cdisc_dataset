namespace P21.Validator.Data;

public interface DataSource : IDisposable
{
    public enum VariableProperty
    {
        Name,
        Type,
        Length,
        Label,
        Order,
        Format
    }

    InternalEntityDetails GetDetails();
    string GetLocation();
    DataSource GetMetadata();
    string GetName();
    int GetRecordCount();
    List<DataRecord> GetRecords();
    List<DataRecord> GetRecords(int recordCount);
    HashSet<string> GetVariables();
    object? GetVariableProperty(string variable, VariableProperty property);
    bool HasRecords();
    bool IsComposite();
    bool IsMetadata();
    DataSource Replicate();
    bool Test();
}
