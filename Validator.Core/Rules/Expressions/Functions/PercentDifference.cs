using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public sealed class PercentDifference : Difference
{
    private const decimal OneHundred = 100m;

    public PercentDifference(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        var difference = base.Compute(record);
        var rhs = GetArgumentValue(record, 1);
        var value = ((decimal)difference.GetValue()) / ((decimal)rhs.GetValue());
        return Factory.Create(Math.Round(value, Function.DivisionScale, MidpointRounding.AwayFromZero) * OneHundred);
    }
}
