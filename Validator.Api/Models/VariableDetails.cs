namespace P21.Validator.Api.Models;

public interface VariableDetails
{
    public enum Property
    {
        Format,
        FullFormat,
        Label,
        Length,
        Name,
        Order,
        Type
    }

    bool GetBoolean(Property property);
    bool? GetBoolean(Property property, bool? defaultValue);
    int GetInteger(Property property);
    int? GetInteger(Property property, int? defaultValue);
    string GetString(Property property);
    string? GetString(Property property, string? defaultValue);
    bool HasProperty(Property property);
}
