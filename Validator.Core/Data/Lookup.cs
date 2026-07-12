using P21.Validator.Core.Rules.Expressions;

namespace P21.Validator.Data;

public interface Lookup
{
    bool Contains(string variable);
    bool Contains(HashSet<string> variables);
    void Release(HashSet<string> variables);
    bool Seek(List<PreparedQuery.Mapping> search, List<PreparedQuery.Mapping> where, bool ignoreWhereFailure);
}
