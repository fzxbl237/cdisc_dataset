using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public sealed class InternalDataDetails : DataDetails
{
    public InternalDataDetails(string name, DataDetails.Info info)
        : this(-1, name, info)
    {
    }

    public InternalDataDetails(int id, DataDetails.Info info)
        : this(id, null, info)
    {
    }

    public InternalDataDetails(int id, string? name, DataDetails.Info info)
        : base(id, info, name)
    {
    }
}
