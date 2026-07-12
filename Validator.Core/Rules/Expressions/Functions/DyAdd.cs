using System.Globalization;
using P21.Validator.Core;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public sealed class DyAdd : AbstractFunction
{
    public DyAdd(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments, 2)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        var lhs = GetArgumentValue(record, 0);
        var rhs = GetArgumentValue(record, 1);

        if (!lhs.HasValue || !lhs.IsDate)
        {
            throw new EvaluationException(Text.Get("Messages.InvalidDate"),
                string.Format(Text.Get("Descriptions.InvalidDate"), Arguments[0], lhs.Type.ToString()));
        }

        if (!rhs.HasValue || !rhs.IsNumeric)
        {
            throw new EvaluationException(Text.Get("Messages.RhsNaN"),
                string.Format(Text.Get("Descriptions.RhsNaN"), rhs.Type.ToString()));
        }

        var lhsDate = DateTime.ParseExact(lhs.ToString()[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var result = lhsDate.AddDays(int.Parse(rhs.ToString(), CultureInfo.InvariantCulture));
        return Factory.Create(result.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
