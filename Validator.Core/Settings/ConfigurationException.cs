namespace P21.Validator.Core.Settings;

public sealed class ConfigurationException : Exception
{
    public enum Type
    {
        GeneralStructure,
        ElementDefinition,
        RuleDefinition,
        GroupDefinition
    }

    public ConfigurationException(Type type, string message)
        : base(message)
    {
        ErrorType = type;
    }

    public ConfigurationException(Type type, Exception cause)
        : base(cause.Message, cause)
    {
        ErrorType = type;
    }

    public ConfigurationException(Type type, string message, Exception cause)
        : base(message, cause)
    {
        ErrorType = type;
    }

    public Type ErrorType { get; }
}
