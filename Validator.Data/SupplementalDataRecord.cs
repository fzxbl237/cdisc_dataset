using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public class SupplementalDataRecord : BaseDataRecord
{
    private readonly DataRecord _record;
    private readonly bool _isTransient;

    public SupplementalDataRecord(DataRecord record, IReadOnlyDictionary<string, DataEntry> supplementalValues, bool isTransient)
        : base(supplementalValues)
    {
        _record = record;
        _isTransient = isTransient;
    }

    public override bool DefinesVariable(string variable)
    {
        return _record.DefinesVariable(variable) || base.DefinesVariable(variable);
    }

    public override DataDetails GetDataDetails() => _record.GetDataDetails();

    public override SourceDetails GetSourceDetails() => _record.GetSourceDetails();

    public override int GetId() => _record.GetId();

    public override DataEntry GetValue(string variable)
    {
        return _record.DefinesVariable(variable) ? _record.GetValue(variable) : base.GetValue(variable);
    }

    public override IReadOnlyCollection<string> GetVariables()
    {
        return _record.GetVariables().Union(base.GetVariables()).ToList();
    }

    public override bool IsTransient(string variable)
    {
        return _record.DefinesVariable(variable) ? _record.IsTransient(variable) : _isTransient;
    }
}
