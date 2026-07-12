namespace P21.Validator.Api.Models;

public sealed class ValidationResult
{
    public ValidationResult(IReadOnlyList<Diagnostic> diagnostics, IReadOnlyCollection<SourceDetails> sources, object? metrics, bool completedSuccessfully)
    {
        Diagnostics = diagnostics;
        Sources = sources;
        Metrics = metrics;
        CompletedSuccessfully = completedSuccessfully;
    }

    public static ValidationResultBuilder Builder()
    {
        return new ValidationResultBuilder();
    }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
    public IReadOnlyCollection<SourceDetails> Sources { get; }
    public object? Metrics { get; }
    public bool CompletedSuccessfully { get; }
}
