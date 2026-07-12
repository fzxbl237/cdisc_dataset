using P21.Validator.Api.Models;

namespace P21.Validator.Api.Validation;

public abstract class BaseValidation : Validation
{
    protected BaseValidation(Models.CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    protected Models.CancellationToken CancellationToken { get; }

    public abstract Models.ValidationResult Run();
}
