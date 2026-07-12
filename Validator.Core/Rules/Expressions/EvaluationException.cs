namespace P21.Validator.Core.Rules.Expressions;

public sealed class EvaluationException : Exception
{
    public EvaluationException(string message, string description)
        : base(message)
    {
        Description = description;
    }

    public string Description { get; }
}
