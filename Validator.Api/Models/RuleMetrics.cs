namespace P21.Validator.Api.Models;

public interface RuleMetrics
{
    IReadOnlyCollection<RuleInstance> GetRules();
}
