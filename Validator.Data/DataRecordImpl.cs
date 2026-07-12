using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public sealed class DataRecordImpl : BaseDataRecord
{
    private readonly InternalEntityDetails _entity;
    private readonly DataDetails _record;

    public DataRecordImpl(DataDetails record, InternalEntityDetails entity, IReadOnlyDictionary<string, DataEntry> values)
        : base(values)
    {
        _record = record;
        _entity = entity;
    }

    public override bool IsTransient(string variable) => false;

    public override DataDetails GetDataDetails() => _record;

    public override SourceDetails GetSourceDetails() => _entity;

    public override int GetId() => _record.Id;
}
