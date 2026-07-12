using P21.Validator.Api.Options;

namespace P21.Validator.Api.Validation;

public interface Validator
{
    Validation CreateValidation(List<SourceOptions> sources, ConfigOptions configOptions, ValidationOptions validationOptions);
}
