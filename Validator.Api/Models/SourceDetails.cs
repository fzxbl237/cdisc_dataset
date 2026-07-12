using P21.Validator.Api.Options;

namespace P21.Validator.Api.Models;

public interface SourceDetails
{
    public enum Property
    {
        Class,
        Configuration,
        Combined,
        Corrupted,
        DatasetLabel,
        FileSize,
        Filtered,
        Keys,
        Label,
        Location,
        Name,
        Records,
        Split,
        Subname,
        Validated,
        Variables
    }

    public enum Reference
    {
        Data,
        Metadata
    }

    bool GetBoolean(Property property);
    bool? GetBoolean(Property property, bool? defaultValue);
    int GetInteger(Property property);
    int? GetInteger(Property property, int? defaultValue);
    SourceDetails? GetParent();
    Reference GetReference();
    string GetString(Property property);
    string? GetString(Property property, string? defaultValue);
    bool HasVariable(string name);
    VariableDetails? GetVariable(string name);
    IReadOnlyList<VariableDetails> GetVariables();
    bool HasProperty(Property property);
    IReadOnlyList<SourceDetails> GetSplitSources();
}
