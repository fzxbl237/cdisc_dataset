using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions.Functions;

public interface Function
{
    public const int DivisionScale = 8;

    DataEntry Compute(DataRecord record);
    HashSet<string> GetVariables();
}
