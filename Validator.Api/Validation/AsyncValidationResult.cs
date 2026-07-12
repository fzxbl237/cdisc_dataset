using P21.Validator.Api.Models;

namespace P21.Validator.Api.Validation;

public sealed class AsyncValidationResult
{
    public AsyncValidationResult(Task<ValidationResult> task)
    {
        Task = task;
    }

    public Task<ValidationResult> Task { get; }
}
