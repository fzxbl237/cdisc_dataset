namespace P21.Validator.Core.Rules;

public sealed class CorruptRuleException : Exception
{
    public enum State
    {
        Unrecoverable,
        Temporary
    }

    public CorruptRuleException(State state, string id, string message, string description)
        : base(message)
    {
        Description = description;
        Id = id;
        CurrentState = state;
    }

    public string Description { get; }

    public string Id { get; }

    public State CurrentState { get; }

    public override string ToString() => $"{Message}: {Description}";
}
