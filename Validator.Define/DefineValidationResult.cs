namespace Validator.Define;

public sealed record DefineValidationResult(IReadOnlyList<DefineDiagnostic> Diagnostics)
{
    public int ErrorCount => Diagnostics.Count(x => x.Severity == DefineDiagnosticSeverity.Error);
    public int WarningCount => Diagnostics.Count(x => x.Severity == DefineDiagnosticSeverity.Warning);
}

public sealed record DefineDiagnostic(
    string RuleId,
    DefineDiagnosticSeverity Severity,
    string Location,
    string Message);

public enum DefineDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record DefineValidationOptions(
    string OdmSchemaPath,
    string DefineSchemaPath);
