using P21.Validator.Api.Models;

namespace P21.Validator.Api.Events;

public interface RuleListener
{
    void Complete() { }
    void AcceptTemplate(RuleTemplate template) { }
    void AcceptInstance(RuleInstance instance) { }
}
