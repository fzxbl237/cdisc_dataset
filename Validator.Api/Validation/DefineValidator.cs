using P21.Validator.Api.Options;

namespace P21.Validator.Api.Validation;

public interface DefineValidator : Validator
{
    Task<Validation> CreateValidationAsync(SourceOptions sourceOptions, ConfigOptions configOptions, ValidationOptions validationOptions);
}
