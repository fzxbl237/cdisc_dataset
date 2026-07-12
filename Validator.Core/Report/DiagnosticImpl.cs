using P21.Validator.Api.Models;

namespace P21.Validator.Core.Report;

public sealed class DiagnosticImpl : Diagnostic
{
    private readonly RuleMetadata _metadata;
    private readonly SourceDetails _sourceDetails;
    private readonly DataDetails _dataDetails;
    private readonly long _creationTimestamp;
    private readonly IReadOnlyDictionary<string, string> _values;

    public DiagnosticImpl(RuleMetadata metadata, SourceDetails sourceDetails, DataDetails dataDetails, IDictionary<string, string> values)
    {
        _metadata = metadata;
        _sourceDetails = sourceDetails;
        _dataDetails = dataDetails;
        _creationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public string GetId() => _metadata.Id;

    public string GetMessage() => _metadata.Message;

    public string GetDescription() => _metadata.Description;

    public string GetCategory() => _metadata.Category;

    public Diagnostic.Type GetType() => _metadata.Type;

    public string? GetContext() => _metadata.Context;

    public long GetTimestamp() => _creationTimestamp;

    public SourceDetails GetSourceDetails() => _sourceDetails;

    public DataDetails GetDataDetails() => _dataDetails;

    public IReadOnlyCollection<string> GetVariables() => _values.Keys.ToList();

    public string GetVariable(string variable) => _values[variable];

    public IReadOnlyDictionary<string, string> GetVariableValues() => _values;

    public IReadOnlyCollection<string> GetProperties() => Array.Empty<string>();

    public string GetProperty(string property) => string.Empty;

    public IReadOnlyDictionary<string, string> GetPropertyValues() => new Dictionary<string, string>();
}
