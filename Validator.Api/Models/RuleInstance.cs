namespace P21.Validator.Api.Models;

public interface RuleInstance : RuleTemplate
{
    string? GetDomain();
    string? GetContext();
}
