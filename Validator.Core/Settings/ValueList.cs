using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

internal sealed class ValueList
{
    private readonly string _oid;
    private readonly List<Clause> _clauses = new();

    public ValueList(string oid)
    {
        _oid = oid;
    }

    public Clause AddClause(string whereItemOid, string mandatory)
    {
        var clause = new Clause(whereItemOid, mandatory);
        _clauses.Add(clause);
        return clause;
    }

    public string GetOid() => _oid;

    public Resolver ResolverFor(Definition variable) => new(this, variable, variable.GetPrefix());

    internal sealed class Resolver
    {
        private readonly ValueList _valueList;
        private readonly Definition _variable;
        private readonly string _prefix;

        public Resolver(ValueList valueList, Definition variable, string prefix)
        {
            _valueList = valueList;
            _variable = variable;
            _prefix = prefix;
        }

        public ValueList GetValueList() => _valueList;

        public Definition GetVariable() => _variable;

        public bool Resolve(KeyMap<Definition> variables)
        {
            var result = true;
            _variable.SetPrefix(_prefix);

            foreach (var clause in _valueList._clauses)
            {
                var target = variables.Get(clause.Oid);
                var expression = clause.GetExpression(variables);

                if (target == null || expression == null)
                {
                    result = false;
                    continue;
                }

                var name = target.GetTargetName();
                _variable.AddDependency(Definition.CreateFrom(clause.Oid, true, target)
                    .SetProperty("Prefix", _prefix)
                    .SetProperty("Expression", expression)
                    .SetProperty("Variable", name)
                    .SetProperty("Mandatory", clause.GetMandatoryFlag())
                ).SetProperty("@Clause", "Y");
            }

            _variable.ClearPrefix();
            return result;
        }
    }

    internal sealed class Clause
    {
        public string Oid { get; }
        private readonly bool _isMandatory;
        private readonly List<Check> _checks = new();

        public Clause(string whereItemOid, string mandatory)
        {
            Oid = whereItemOid;
            _isMandatory = string.Equals(mandatory, "yes", StringComparison.OrdinalIgnoreCase);
        }

        public Clause AddCheck(string checkItemOid, string comparator, HashSet<string> values)
        {
            _checks.Add(new Check(checkItemOid, comparator, values));
            return this;
        }

        public string GetMandatoryFlag() => _isMandatory ? "Y" : "N";

        public string? GetExpression(KeyMap<Definition> variables)
        {
            if (_checks.Count == 0)
            {
                return null;
            }

            var buffer = new System.Text.StringBuilder();
            var shouldWrap = _checks.Count > 1;
            var isFirst = true;

            foreach (var check in _checks)
            {
                if (!isFirst || (isFirst = false))
                {
                    buffer.Append(" @and ");
                }

                if (shouldWrap)
                {
                    buffer.Append("(");
                }

                var resolved = check.Resolve(variables);
                if (resolved == null)
                {
                    return null;
                }

                buffer.Append(resolved);
                if (shouldWrap)
                {
                    buffer.Append(")");
                }
            }

            return buffer.ToString();
        }

        private sealed class Check
        {
            private static readonly HashSet<string> Comparators = new(StringComparer.OrdinalIgnoreCase)
            {
                "lt", "le", "gt", "ge", "eq", "ne", "in", "notin"
            };

            private readonly string _oid;
            private readonly bool _matchAll;
            private readonly bool _isValid;
            private readonly List<string> _expressions = new();

            public Check(string oid, string comparator, HashSet<string> values)
            {
                _oid = oid;
                comparator = comparator.Trim().ToLowerInvariant();

                var isValid = Comparators.Contains(comparator);
                if (values.Count > 1 && comparator is not ("in" or "notin"))
                {
                    isValid = false;
                }

                _isValid = isValid;
                _matchAll = comparator == "notin";

                if (_isValid)
                {
                    var op = "==";
                    if (comparator is "ne" or "notin")
                    {
                        op = "!=";
                    }
                    else if (comparator is not ("eq" or "in"))
                    {
                        op = "@" + comparator;
                    }

                    foreach (var value in values)
                    {
                        _expressions.Add($" {op} '{value.Replace("'", "\\'")}'");
                    }
                }
            }

            public string? Resolve(KeyMap<Definition> variables)
            {
                if (!_isValid)
                {
                    return null;
                }

                var variable = variables.Get(_oid);
                if (variable == null)
                {
                    return null;
                }

                var isFirst = true;
                var buffer = new System.Text.StringBuilder();

                foreach (var expression in _expressions)
                {
                    if (!isFirst || (isFirst = false))
                    {
                        buffer.Append(_matchAll ? " @and " : " @or ");
                    }

                    buffer.Append(variable.GetTargetName()).Append(expression);
                }

                return buffer.ToString();
            }
        }
    }
}
