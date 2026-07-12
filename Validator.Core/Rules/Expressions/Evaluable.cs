using P21.Validator.Data;

namespace P21.Validator.Core.Rules.Expressions;

public interface Evaluable
{
    bool Evaluate(DataRecord record);
    HashSet<string> GetVariables();
}
