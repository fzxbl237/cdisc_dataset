namespace P21.Validator.Api.Models;

public sealed class ValidationResultBuilder
{
    private readonly List<Diagnostic> _diagnostics = new();
    private readonly HashSet<SourceDetails> _sources = new();
    private object? _metrics;
    private bool _completedSuccessfully = true;

    public ValidationResultBuilder WithDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        _diagnostics.Clear();
        _diagnostics.AddRange(diagnostics);
        return this;
    }

    public ValidationResultBuilder AddDiagnostic(Diagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
        return this;
    }

    public ValidationResultBuilder AddDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        _diagnostics.AddRange(diagnostics);
        return this;
    }

    public ValidationResultBuilder AddSource(SourceDetails source)
    {
        _sources.Add(source);
        return this;
    }

    public ValidationResultBuilder WithSources(IEnumerable<SourceDetails> sources)
    {
        _sources.Clear();
        foreach (var s in sources)
        {
            _sources.Add(s);
        }

        return this;
    }

    public ValidationResultBuilder WithMetrics(object? metrics)
    {
        _metrics = metrics;
        return this;
    }

    public ValidationResultBuilder WithCompletedSuccessfully(bool completedSuccessfully)
    {
        _completedSuccessfully = completedSuccessfully;
        return this;
    }

    public ValidationResult Build()
    {
        return new ValidationResult(_diagnostics.AsReadOnly(), _sources.ToList().AsReadOnly(), _metrics, _completedSuccessfully);
    }
}
