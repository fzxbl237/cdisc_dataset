namespace Net.Pinnacle21.Define.Parser;

public sealed class SyntaxException : Exception
{
    public SyntaxException(string message, Position position)
        : base(message)
    {
        Position = position;
    }

    public Position Position { get; }
}
