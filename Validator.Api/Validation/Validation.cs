using P21.Validator.Api.Models;

namespace P21.Validator.Api.Validation;

public interface Validation
{
    Models.ValidationResult Run();
}
