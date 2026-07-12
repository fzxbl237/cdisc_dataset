using P21.Validator.Api.Models;

namespace P21.Validator.Api.Events;

public interface DiagnosticListener
{
    void OnDiagnostic(Diagnostic diagnostic);
}
