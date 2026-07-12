namespace P21.Validator.Data;

public sealed class InvalidDataException : Exception
{
    public enum Codes
    {
        CannotParseSource,
        NoVariables,
        NoRecords,
        InvalidRequestState,
        DataDimensionMismatch,
        MissingSource,
        DuplicateVariable
    }

    public InvalidDataException(Codes errorCode) : this(errorCode, null)
    {
    }

    public InvalidDataException(Codes errorCode, string? message) : base(message)
    {
        Code = errorCode;
    }

    public Codes Code { get; }
}
