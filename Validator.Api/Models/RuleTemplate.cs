namespace P21.Validator.Api.Models;

public interface RuleTemplate
{
    string GetId();
    string GetPublisherId();
    string GetCategory();
    string GetMessage();
    string GetDescription();
    Diagnostic.Type GetType();
}
