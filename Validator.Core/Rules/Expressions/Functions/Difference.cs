using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public class Difference : AbstractFunction
{
    public Difference(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments, 2)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        var lhs = GetArgumentValue(record, 0);
        var rhs = GetArgumentValue(record, 1);
        return Factory.Create((decimal)lhs.GetValue() - (decimal)rhs.GetValue());
    }
}
