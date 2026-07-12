namespace P21.Validator.Core.Util;

public sealed class ErrorAccumulator
{
    private readonly Stack<Context> _context = new();
    private readonly List<ErrorMessage> _errors = new();

    public ErrorAccumulator(string type, string name)
    {
        StartContext(type, name);
    }

    public void Record(string message)
    {
        _errors.Add(new ErrorMessage(_context.Reverse().ToArray(), message));
    }

    public void StartContext(string type, string name)
    {
        _context.Push(new Context(type, name));
    }

    public void EndContext()
    {
        if (_context.Count > 0)
        {
            _context.Pop();
        }
    }

    public bool HasErrors()
    {
        return _errors.Count == 0;
    }

    public override string ToString()
    {
        return string.Join("\n", _errors.Select(error => error.ToString()));
    }

    private sealed class Context
    {
        private readonly string _type;
        private readonly string _name;

        public Context(string type, string name)
        {
            _type = type;
            _name = name;
        }

        public override string ToString() => $"{_type}[{_name}]";
    }

    private sealed class ErrorMessage
    {
        private readonly Context[] _context;
        private readonly string _message;

        public ErrorMessage(Context[] context, string message)
        {
            _context = context;
            _message = message;
        }

        public override string ToString()
        {
            return $"{string.Join(" -> ", _context.Select(item => item.ToString()))}: {_message}";
        }
    }
}
