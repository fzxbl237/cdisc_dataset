namespace P21.Validator.Core.Settings;

public sealed class RuntimeException : Exception
{
    public RuntimeException(string message) : base(message)
    {
    }

    public RuntimeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
