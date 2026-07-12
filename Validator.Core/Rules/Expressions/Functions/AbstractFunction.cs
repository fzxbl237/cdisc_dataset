using System.Text.RegularExpressions;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public abstract class AbstractFunction : Function
{
    public abstract DataEntry Compute(DataRecord record);

    private const string VariablePattern = "[A-Za-z][A-Za-z0-9]*|(?:VAL|SUB|VAR):[A-Za-z0-9]+";

    protected readonly string Name;
    protected readonly string[] Arguments;
    protected readonly DataEntry[] Constants;
    protected readonly HashSet<string> Variables;
    protected readonly DataEntryFactory Factory;

    protected AbstractFunction(string name, DataEntryFactory factory, string[] arguments, int expected, bool unbounded = false)
    {
        if (arguments.Length != expected && (!unbounded || arguments.Length < expected))
        {
            throw new SyntaxException($"The {name} function requires {(unbounded ? "at least " : string.Empty)}{expected} arguments; {arguments.Length} given");
        }

        Name = name;
        Arguments = arguments;
        Factory = factory;
        Constants = new DataEntry[arguments.Length];

        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Arguments.Length; ++i)
        {
            var argument = Arguments[i].Trim();
            Arguments[i] = argument;

            if (Regex.IsMatch(argument, VariablePattern))
            {
                variables.Add(argument);
            }
            else
            {
                if (argument.StartsWith("'"))
                {
                    argument = argument.Substring(1, argument.Length - 2);
                }

                Constants[i] = factory.Create(argument);
            }
        }

        Variables = variables;
    }

    public HashSet<string> GetVariables() => Variables;

    public override string ToString()
    {
        return $":{Name}({string.Join(',', Arguments)})";
    }

    protected DataEntry GetArgumentValue(DataRecord record, int argumentIndex)
    {
        return Constants[argumentIndex] ?? record.GetValue(Arguments[argumentIndex]);
    }
}
