namespace P21.Validator.Core.Settings;

public sealed class MagicVariableSyntaxException : ArgumentException
{
    public MagicVariableSyntaxException(string property, string syntax, string message)
        : base(message)
    {
        Property = property;
        Syntax = syntax;
    }

    public string Property { get; }

    public string Syntax { get; }

    public override string ToString()
    {
        return $"Property '{Property}' contains syntax error in '{Syntax}': {Message}";
    }
}
