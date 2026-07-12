using System.Text.RegularExpressions;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions;

public sealed class PreparedQuery
{
    private const string ComplexVariablePattern = "(" +
        "[A-Za-z]?" +
        "(?:" +
            "(?<=[A-Za-z])" +
            "[A-Za-z0-9]*" +
        ")?" +
        "(?:" +
            "\\[" +
            "([A-Za-z][A-Za-z0-9]*)" +
            "(?:" +
              "\\:" +
              "([0-9](?:L|R)[^\\]])" +
            ")?" +
            "\\]" +
        ")?" +
        "(?<!\\A)" +
        "[A-Za-z0-9]*" +
        "|VAL:[A-Za-z0-9]+" +
        ")";
    private const string Literal = "(" +
        "'(?:[^']|\\\\')+'" +
        "|" +
        "(?:-?[0-9]+(?:\\.[0-9]+)?)" +
        "|" +
        "null" +
        ")";
    private const string Operators = "([<>]|[=<>!^]=)";
    private const string SimpleVariable = "([A-Za-z][A-Za-z0-9]+)";
    private const string ClausePattern = "(\\b|(?<=[^\\w\\s]))\\s*(?=\\&{2}|\\|{2})";

    private bool _requestable = true;
    private readonly Regex _clause = new(
        "(\\&{2}|\\|{2})?" +
        "\\s*" +
        ComplexVariablePattern +
        "\\s*" +
        Operators +
        "\\s*" +
        "(?:" +
          "\\[" +
          SimpleVariable +
          "\\]" +
          "|" +
          Literal +
        ")");

    private readonly List<Mapping> _searches = new();
    private readonly List<Mapping> _where = new();
    private readonly ComplexVariable _target;

    public PreparedQuery(string target, string? search)
    {
        var pattern = new Regex(ComplexVariablePattern);
        var matcher = pattern.Match(target);
        if (matcher.Success)
        {
            _target = new ComplexVariable(matcher.Groups[2].Value, matcher.Groups[1].Value, matcher.Groups[3].Value);
        }
        else
        {
            throw new ArgumentException($"target {target} is not properly formatted");
        }

        if (!string.IsNullOrEmpty(search))
        {
            var associations = search.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var association in associations)
            {
                matcher = pattern.Match(association.Trim());
                if (!matcher.Success)
                {
                    throw new ArgumentException($"The remote variable '{association}' is not properly formatted");
                }

                _searches.Add(new Mapping(
                    new ComplexVariable(matcher.Groups[2].Value, matcher.Groups[1].Value, matcher.Groups[3].Value),
                    null,
                    null,
                    null));
            }
        }
    }

    public PreparedQuery(string target, string search, string? where, DataEntryFactory factory, bool strict)
    {
        var pattern = new Regex(ComplexVariablePattern);
        var matcher = pattern.Match(target);
        if (matcher.Success)
        {
            _target = new ComplexVariable(matcher.Groups[2].Value, matcher.Groups[1].Value, matcher.Groups[3].Value);
        }
        else if (target.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || !strict)
        {
            _target = new ComplexVariable(null, target, null);
        }
        else
        {
            throw new ArgumentException($"target {target} is not properly formatted");
        }

        if (!string.IsNullOrEmpty(search))
        {
            ParseInto(search, _searches, factory);
            if (!string.IsNullOrEmpty(where))
            {
                ParseInto(where, _where, factory);
            }
        }
    }

    private void ParseInto(string query, List<Mapping> mappings, DataEntryFactory factory)
    {
        query = query.Replace("''", "null")
            .Replace("@and", "&&")
            .Replace("@gteq", ">=")
            .Replace("@lteq", "<=")
            .Replace("@lt", "<")
            .Replace("@gt", ">")
            .Replace("@or", "||")
            .Replace("@eqic", "^=")
            .Replace("@neqic", "!=");

        var clauses = Regex.Split(query, ClausePattern);
        foreach (var clause in clauses)
        {
            if(string.IsNullOrWhiteSpace(clause)) continue;
            var matcher = _clause.Match(clause);
            if (!matcher.Success)
            {
                throw new ArgumentException($"The clause '{clause}' is not properly formatted");
            }

            Mapping.Operator? op = null;
            Mapping.Comparator? comparator = null;
            var operatorGroup = matcher.Groups[1].Value;
            var comparatorGroup = matcher.Groups[5].Value;

            if (!string.IsNullOrEmpty(operatorGroup))
            {
                op = operatorGroup == "&&" ? Mapping.Operator.And : Mapping.Operator.Or;
            }

            comparator = comparatorGroup switch
            {
                "==" => Mapping.Comparator.EQ,
                "^=" => Mapping.Comparator.EIQ,
                "!=" => Mapping.Comparator.NEQ,
                "<" => Mapping.Comparator.LT,
                "<=" => Mapping.Comparator.LTE,
                ">" => Mapping.Comparator.GT,
                ">=" => Mapping.Comparator.GTE,
                _ => comparator
            };

            var mapping = new Mapping(
                new ComplexVariable(matcher.Groups[3].Value, matcher.Groups[2].Value, matcher.Groups[4].Value),
                comparator,
                matcher.Groups[6].Value,
                op);

            var literal = matcher.Groups[7].Value;
            if (!string.IsNullOrEmpty(literal))
            {
                mapping.SetValue(factory.Create(literal == "null" ? null : literal.Trim('\'')));
            }

            mappings.Add(mapping);
        }
    }

    public List<Mapping> GetSearch(DataRecord record) => GetMappings(_searches, record);

    public string GetTarget() => GetTarget(null);

    public string GetTarget(DataRecord? record) => _target.GetVariable(record);

    public HashSet<string> GetLocal()
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_target.Local != null)
        {
            variables.Add(_target.Local);
            _requestable = false;
        }

        foreach (var mapping in _searches)
        {
            if (mapping.Local != null)
            {
                variables.Add(mapping.Local);
            }

            if (mapping.Remote.Local != null)
            {
                variables.Add(mapping.Remote.Local);
                _requestable = false;
            }
        }

        foreach (var mapping in _where)
        {
            if (mapping.Local != null)
            {
                variables.Add(mapping.Local);
            }

            if (mapping.Remote.Local != null)
            {
                variables.Add(mapping.Remote.Local);
                _requestable = false;
            }
        }

        return variables;
    }

    public HashSet<string> GetRemote()
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in _searches)
        {
            variables.Add(mapping.Remote.Template.ToUpperInvariant());
        }

        foreach (var mapping in _where)
        {
            variables.Add(mapping.Remote.Template.ToUpperInvariant());
        }

        return variables;
    }

    public List<Mapping> GetWhere(DataRecord record) => GetMappings(_where, record);

    public bool IsRequestable() => _requestable;

    private List<Mapping> GetMappings(List<Mapping> mappings, DataRecord record)
    {
        foreach (var mapping in mappings)
        {
            var local = mapping.GetLocal();
            mapping.GetRemote(record);

            if (local != null)
            {
                mapping.SetValue(record.GetValue(local));
            }
        }

        return mappings;
    }

    public sealed class Mapping
    {
        public enum Comparator
        {
            EQ,
            EIQ,
            NEQ,
            LT,
            GT,
            LTE,
            GTE
        }

        public enum Operator
        {
            And,
            Or
        }

        private readonly Comparator? _comparator;
        private string? _last;
        private readonly Operator? _operator;
        private DataEntry? _value;

        public Mapping(ComplexVariable remote, Comparator? comparator, string? local, Operator? op)
        {
            Remote = remote;
            _comparator = comparator;
            Local = local;
            _operator = op;
        }

        public string? Local { get; }
        public ComplexVariable Remote { get; }
        public Comparator? GetComparator() => _comparator;
        public string? GetLocal() => Local;
        public Operator? GetOperator() => _operator;
        public string? GetRemote() => _last;

        public string GetRemote(DataRecord record)
        {
            _last = Remote.GetVariable(record).ToUpperInvariant();
            return _last;
        }

        public DataEntry? GetValue() => _value;

        public void SetValue(DataEntry value) => _value = value;
    }

    public sealed class ComplexVariable
    {
        public ComplexVariable(string? local, string template, string? padding)
        {
            Local = local;
            Template = local != null ? Regex.Replace(template, "\\[[^\\]]+\\]", "%s%s", RegexOptions.None) : template;
            Padding = !string.IsNullOrWhiteSpace(padding) ? new Padding(padding) : null;
        }

        public string? Local { get; }
        public string Template { get; }
        public Padding? Padding { get; }
        private string? _cache;
        private string? _last;

        public string GetVariable(DataRecord? record)
        {
            if (Local == null || record == null)
            {
                return Template;
            }

            var insert = record.GetValue(Local).ToString();
            if (insert == _last)
            {
                return _cache ?? Template;
            }

            var left = true;
            var padding = string.Empty;

            if (Padding != null)
            {
                left = Padding.Left;
                var difference = Padding.Times - insert.Length;
                if (difference > 0)
                {
                    padding = new string(Padding.PaddingChar, difference);
                }
            }

            _cache = string.Format(Template, left ? padding : insert, left ? insert : padding);
            _last = insert;
            return _cache;
        }
    }

    public sealed class Padding
    {
        public Padding(string padding)
        {
            if (padding.Length != 3)
            {
                throw new ArgumentException("Invalid padding string");
            }

            Times = byte.Parse(padding[..1]);
            Left = !padding.Substring(1, 1).Equals("R", StringComparison.Ordinal);
            PaddingChar = padding[2];
        }

        public byte Times { get; }
        public bool Left { get; }
        public char PaddingChar { get; }
    }
}
