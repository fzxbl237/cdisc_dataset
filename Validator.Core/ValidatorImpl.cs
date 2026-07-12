using P21.Validator.Api.Validation;
using P21.Validator.Api.Options;

namespace P21.Validator.Core;

public sealed class ValidatorImpl : DataValidator
{
    public bool Supports(string sourceType) => true;

    public Validation CreateValidation(List<SourceOptions> sources, ConfigOptions configOptions, ValidationOptions validationOptions)
    {
        return new ValidationImpl(sources, configOptions, validationOptions, null);
    }

    public async Task<Validation> CreateValidationAsync(List<SourceOptions> sources, ConfigOptions configOptions, ValidationOptions validationOptions)
    {
        return await Task.FromResult(CreateValidation(sources, configOptions, validationOptions));
    }

    public Validation Prepare(List<SourceOptions> sources, ConfigOptions config, ValidationOptions options, TaskScheduler? sharedScheduler)
    {
        return new ValidationImpl(sources, config, options, sharedScheduler);
    }

    public bool SupportsFeature(string feature) => false;
    
}
