namespace P21.Validator.Api.Models;

public interface Diagnostic
{
    public enum Type
    {
        Reject,
        Error,
        Warning,
        Notice
    }

    string GetId();
    string GetMessage();
    string GetDescription();
    string GetCategory();
    Type GetType();
    string? GetContext();
    long GetTimestamp();
    SourceDetails GetSourceDetails();
    DataDetails GetDataDetails();
    IReadOnlyCollection<string> GetVariables();
    string GetVariable(string variable);
    IReadOnlyDictionary<string, string> GetVariableValues();
    IReadOnlyCollection<string> GetProperties();
    string GetProperty(string property);
    IReadOnlyDictionary<string, string> GetPropertyValues();

    public static Type FromString(string value)
    {
        if (string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase))
        {
            return Type.Error;
        }

        if (string.Equals(value, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            return Type.Reject;
        }

        if (string.Equals(value, "Information", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Notice", StringComparison.OrdinalIgnoreCase))
        {
            return Type.Notice;
        }

        return Type.Warning;
    }
}
