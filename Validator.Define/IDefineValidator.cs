namespace Validator.Define;

public interface IDefineValidator
{
    DefineValidationResult Validate(string xml, DefineValidationOptions options);
}
