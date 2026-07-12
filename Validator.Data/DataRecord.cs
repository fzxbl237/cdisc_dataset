using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public interface DataRecord
{
    bool DefinesVariable(string variable);
    bool IsTransient(string variable);
    DataDetails GetDataDetails();
    SourceDetails GetSourceDetails();
    DataEntry GetValue(string variable);
    IReadOnlyCollection<string> GetVariables();
    int GetId();
}
