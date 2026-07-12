namespace P21.Validator.Api.Models;

public class DataDetails
{
    public enum Info
    {
        Data,
        Metadata,
        Variable
    }

    public DataDetails(int id, Info info, string? name = null)
    {
        Id = id;
        DataInfo = info;
        Name = name;
    }

    public int Id { get; }

    public Info DataInfo { get; }

    public string? Name { get; }

    public string? Key { get; private set; }

    public void SetKey(string? key)
    {
        Key = key;
    }
}
