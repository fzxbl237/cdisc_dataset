using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public sealed class Maximum : AbstractFunction
{
    public Maximum(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments, 1, true)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        DataEntry? maximum = null;
        foreach (var variable in Variables)
        {
            var current = record.GetValue(variable);
            if (maximum == null || maximum.CompareToAny(current) < 0)
            {
                maximum = current;
            }
        }

        return maximum ?? DataEntry.NullEntry;
    }
}
