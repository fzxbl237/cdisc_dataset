using P21.Validator.Core;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public sealed class Division : AbstractFunction
{
    public Division(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments, 2)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        var numerator = GetArgumentValue(record, 0);
        var denominator = GetArgumentValue(record, 1);

        if (!numerator.HasValue|| !numerator.IsNumeric)
        {
            throw new EvaluationException(Text.Get("Messages.NumeratorNaN"),
                string.Format(Text.Get("Descriptions.NumeratorNaN"), numerator.Type.ToString()));
        }

        if (!denominator.HasValue || !denominator.IsNumeric)
        {
            throw new EvaluationException(Text.Get("Messages.DenominatorNaN"),
                string.Format(Text.Get("Descriptions.DenominatorNaN"), numerator.Type.ToString()));
        }

        var value = (decimal)numerator.GetValue() / (decimal)denominator.GetValue();
        return Factory.Create(Math.Round(value, Function.DivisionScale, MidpointRounding.AwayFromZero));
    }
}
