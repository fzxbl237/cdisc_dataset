using P21.Validator.Api.Options;

namespace P21.Validator.Api.Validation;

public interface DataValidator : Validator
{
    bool Supports(string sourceType);
    Task<Validation> CreateValidationAsync(List<SourceOptions> sources, ConfigOptions configOptions, ValidationOptions validationOptions);
}
