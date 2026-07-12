namespace P21.Validator.Core.Rules.Expressions;

public sealed class SyntaxException : Exception
{
    public SyntaxException(string message)
        : base(message)
    {
    }

    public SyntaxException(Exception inner)
        : base(inner.Message, inner)
    {
    }
}
