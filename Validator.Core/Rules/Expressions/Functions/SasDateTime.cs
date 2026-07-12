using P21.Validator.Core;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public sealed class SasDateTime : AbstractFunction
{
    private static readonly decimal TimeFactor = 60m * 60m * 24m;

    public SasDateTime(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments, 1)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        var timestamp = record.GetValue(Arguments[0]);
        if (!timestamp.HasValue || !timestamp.IsNumeric)
        {
            throw new EvaluationException(Text.Get("Messages.TimestampNaN"),
                string.Format(Text.Get("Descriptions.TimestampNaN"), timestamp.Type.ToString()));
        }

        var computed = (decimal)timestamp.GetValue();
        computed = Name.Equals("TIME", StringComparison.OrdinalIgnoreCase)
            ? computed % TimeFactor
            : Math.Floor(computed / TimeFactor) * TimeFactor;

        return Factory.Create(computed);
    }
}
