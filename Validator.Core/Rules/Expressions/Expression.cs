using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Api.Options;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions;

public sealed class Expression : Evaluable
{
    private enum Operator
    {
        And,
        Or
    }

    private readonly List<Evaluable> _evaluationStack = new();
    private readonly List<Operator> _operatorStack = new();
    private readonly bool _negated;
    private HashSet<string>? _variables;

    public static Expression CreateFrom(string raw, ValidationOptions options, DataEntryFactory factory)
    {
        raw = Regex.Replace(raw, "(?<!\\\\)''", "null")
            .Replace("@ge", "@gteq")
            .Replace("@le", "@lteq")
            .Replace("@and", "&&")
            .Replace("@gteq", ">=")
            .Replace("@lteq", "<=")
            .Replace("@lt", "<")
            .Replace("@gt", ">")
            .Replace("@or", "||")
            .Replace("@eqic", "^=")
            .Replace("@neqic", ".=")
            .Replace("@re", "~=")
            .Replace("@feq", "%=");

        return new Expression(raw, options, factory);
    }

    public Expression(string expression, ValidationOptions options, DataEntryFactory factory)
        : this(expression, options, factory, false)
    {
        _variables = GetVariables();
    }

    private Expression(string expression, ValidationOptions options, DataEntryFactory factory, bool negated)
    {
        if (expression == null)
        {
            throw new SyntaxException("A null expression cannot be evaluated");
        }

        expression = expression.Trim();
        if (expression.Length == 0)
        {
            throw new SyntaxException("An empty expression cannot be evaluated");
        }

        _negated = negated;

        var functions = new List<string>();
        var functionPattern = new Regex(":[A-Z]+\\([^)]+\\)");
        foreach (Match match in functionPattern.Matches(expression))
        {
            var function = match.Value;
            functions.Add(function);
            expression = expression.Replace(function, "~" + (functions.Count - 1));
        }

        var constants = new List<string>();
        var constantPattern = new Regex("(?:([<>]|[.^=<>!~]=)\\s*)('(?:\\\\'|[^'])+'|-?[0-9]+(?:\\.[0-9]+)?|null)");
        foreach (Match match in constantPattern.Matches(expression))
        {
            var constant = match.Groups[2].Value;
            constants.Add(constant);
            expression = expression.Replace(constant, "$" + (constants.Count - 1));
        }

        var start = -1;
        var opened = 0;
        var tracking = false;
        var originalExpression = expression;
        var expressions = new List<string>();
        var enclosurePattern = new Regex("!?\\(|\\)");

        foreach (Match match in enclosurePattern.Matches(expression))
        {
            var token = match.Value;
            if (tracking)
            {
                if (token != ")")
                {
                    opened++;
                }
                else
                {
                    opened--;
                }

                if (opened == 0)
                {
                    var finish = match.Index + match.Length;
                    var subexpression = originalExpression.Substring(start, finish - start);
                    expressions.Add(subexpression);
                    expression = Regex.Replace(expression, Regex.Escape(subexpression), "%" + (expressions.Count - 1), RegexOptions.None, TimeSpan.FromSeconds(1));
                    tracking = false;
                }
                else if (opened < 0)
                {
                    throw new SyntaxException("Misplaced closing parenthesis");
                }
            }
            else if (token != ")")
            {
                opened++;
                start = match.Index;
                tracking = true;
            }
            else
            {
                throw new SyntaxException("Misplaced closing parenthesis");
            }
        }

        var operatorPattern = new Regex(@"\|{2}|&{2}");
        foreach (Match match in operatorPattern.Matches(expression))
        {
            _operatorStack.Add(match.Value == "&&" ? Operator.And : Operator.Or);
        }

        var components = Regex.Split(expression, @"\|{2}|&{2}");
        foreach (var rawComponent in components)
        {
            var component = rawComponent.Trim();
            var isExpression = false;

            if (component.StartsWith("%"))
            {
                isExpression = true;
                // int.TryParse(component[1..],out int index);
                // var xx = index;
                var index = int.Parse(component[1..]);
                component = expressions[index];
            }

            for (var j = functions.Count - 1; j > -1; --j)
            {
                component = component.Replace("~" + j, functions[j]);
            }

            for (var j = constants.Count - 1; j > -1; --j)
            {
                component = component.Replace("$" + j, constants[j]);
            }

            if (isExpression)
            {
                var isNegated = component.StartsWith("!");
                component = component.Substring(component.IndexOf('(') + 1);
                component = component.Substring(0, component.LastIndexOf(')'));
                _evaluationStack.Add(new Expression(component, options, factory, isNegated));
            }
            else
            {
                _evaluationStack.Add(Comparison.CreateComparison(component, options, factory));
            }
        }
    }

    public bool Evaluate(DataRecord record)
    {
        var result = true;
        for (var i = 0; i < _evaluationStack.Count; ++i)
        {
            var subresult = _evaluationStack[i];
            if (i == 0)
            {
                result = subresult.Evaluate(record);
            }
            else if (_operatorStack[i - 1] == Operator.And)
            {
                if (result)
                {
                    result = subresult.Evaluate(record);
                }
            }
            else
            {
                if (result)
                {
                    break;
                }

                if (i < _operatorStack.Count && _operatorStack[i] == Operator.And)
                {
                    var combinedResult = false;
                    var startPosition = i;
                    for (var j = startPosition; j < _operatorStack.Count && _operatorStack[j] == Operator.And; ++j, i = j + 1)
                    {
                        var subsubresult = _evaluationStack[j + 1];
                        if (j == startPosition)
                        {
                            combinedResult = subresult.Evaluate(record) && subsubresult.Evaluate(record);
                        }
                        else if (combinedResult)
                        {
                            combinedResult = subsubresult.Evaluate(record);
                        }
                    }

                    result = combinedResult;
                }
                else
                {
                    result = subresult.Evaluate(record);
                }
            }
        }

        return _negated != result;
    }

    public override string ToString()
    {
        var result = string.Empty;
        for (var i = 0; i < _evaluationStack.Count; ++i)
        {
            if (i == 0)
            {
                result += _evaluationStack[i];
            }
            else
            {
                result += " " + _operatorStack[i - 1] + " " + _evaluationStack[i];
            }
        }

        return (_negated ? "!" : string.Empty) + "(" + result + ")";
    }

    public HashSet<string> GetVariables()
    {
        if (_variables != null)
        {
            return _variables;
        }

        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var level in _evaluationStack)
        {
            foreach (var variable in level.GetVariables())
            {
                variables.Add(variable);
            }
        }

        return variables;
    }
}
