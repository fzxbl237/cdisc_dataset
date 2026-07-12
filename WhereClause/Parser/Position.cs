namespace Net.Pinnacle21.Define.Parser;

public sealed class Position
{
    private readonly string _expression;
    private readonly int _position;

    public Position(string expression, int position)
    {
        _expression = expression;
        _position = position;
    }

    public string FullExpression => _expression;
    public int Offset => _position;
    public string Value => _expression.Substring(_position);

    public Position TrimStart()
    {
        var index = _position;
        while (index < _expression.Length && char.IsWhiteSpace(_expression[index])) index++;
        return new Position(_expression, index);
    }

    public bool IsEmpty() => _position == _expression.Length;
    public Position At(int length) => new Position(_expression, _position + length);
    public bool StartsWith(string value) => Value.StartsWith(value);
}
