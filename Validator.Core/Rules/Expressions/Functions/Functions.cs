using System.Reflection;
using P21.Validator.Data;
using P21.Validator.Core.Settings;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public static class Functions
{
    private static readonly Dictionary<string, Type> FunctionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DATE"] = typeof(SasDateTime),
        ["TIME"] = typeof(SasDateTime),
        ["MAX"] = typeof(Maximum),
        ["DIV"] = typeof(Division),
        ["DY"] = typeof(DyCount),
        ["DIFF"] = typeof(Difference),
        ["PCTDIFF"] = typeof(PercentDifference),
        ["DYADD"] = typeof(DyAdd)
    };

    public static Function Create(string function, DataEntryFactory factory)
    {
        if (!function.StartsWith(":", StringComparison.Ordinal))
        {
            throw new SyntaxException($"'{function}' isn't a function");
        }

        function = function[1..];
        var start = function.IndexOf('(');
        if (start == -1 || !function.EndsWith(")", StringComparison.Ordinal))
        {
            throw new SyntaxException($"'{function}' doesn't have the correct parentheses");
        }

        var name = function[..start];
        var args = function[(start + 1)..^1];
        if (!FunctionMap.TryGetValue(name, out var definition))
        {
            throw new SyntaxException($"Unknown function '{name}'");
        }

        try
        {
            return (Function)Activator.CreateInstance(definition, name, factory, args.Split(','))!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is SyntaxException)
        {
            throw ex.InnerException;
        }
        catch (Exception ex)
        {
            throw new RuntimeException($"Unable to create function {name}", ex);
        }
    }
}
