using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public static class Create
{
    public static Function CreateFunction(string function, DataEntryFactory factory)
    {
        return Functions.Create(function, factory);
    }
}
