namespace P21.Validator.Core.Settings;

public sealed class PrototypeCriteria
{
    private readonly string? _datasetName;
    private readonly List<string> _variables;

    public PrototypeCriteria(string? datasetName, string[] keyVariables)
    {
        _datasetName = datasetName;
        _variables = keyVariables.ToList();
    }

    public string? GetDatasetName() => _datasetName;

    public bool HasDatasetName() => !string.IsNullOrEmpty(_datasetName);

    public IReadOnlyList<string> GetVariables() => _variables;

    public bool HasVariables() => _variables.Count > 0;

    public bool IsFallbackCriteria()
    {
        return !HasDatasetName() && _variables.Count == 1 && _variables[0] == "*";
    }
}
