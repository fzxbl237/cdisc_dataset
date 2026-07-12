using System.Globalization;
using P21.Validator.Core;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public sealed class DyCount : AbstractFunction
{
    public DyCount(string name, DataEntryFactory factory, string[] arguments)
        : base(name, factory, arguments, 2)
    {
    }

    public override DataEntry Compute(DataRecord record)
    {
        var reference = GetArgumentValue(record, 0);
        var recorded = GetArgumentValue(record, 1);

        if (!reference.HasValue || !reference.IsDate)
        {
            throw new EvaluationException(Text.Get("Messages.InvalidDate"),
                string.Format(Text.Get("Descriptions.InvalidDate"), Arguments[0], reference.Type.ToString()));
        }

        if (!recorded.HasValue || !recorded.IsDate)
        {
            throw new EvaluationException(Text.Get("Messages.InvalidDate"),
                string.Format(Text.Get("Descriptions.InvalidDate"), Arguments[1], recorded.Type.ToString()));
        }

        var referenceDate = DateTime.ParseExact(reference.ToString()[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var recordedDate = DateTime.ParseExact(recorded.ToString()[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var days = (recordedDate - referenceDate).Days;
        return Factory.Create(days < 0 ? days : days + 1);
    }
}
