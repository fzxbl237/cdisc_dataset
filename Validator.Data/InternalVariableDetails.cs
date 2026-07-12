using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public sealed class InternalVariableDetails : PropertySet<VariableDetails.Property>, VariableDetails
{
    public InternalVariableDetails(string name, int order)
        : base()
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("name cannot be null", nameof(name));
        }

        SetProperty(VariableDetails.Property.Name, name);
        SetProperty(VariableDetails.Property.Order, order);
    }

    public InternalVariableDetails(string name, int order, string? type, int? length, string? label, string? format, string? fullFormat)
        : this(name, order)
    {
        SetProperty(VariableDetails.Property.Type, type);
        SetProperty(VariableDetails.Property.Length, length);
        SetProperty(VariableDetails.Property.Label, label);
        SetProperty(VariableDetails.Property.Format, format);
        SetProperty(VariableDetails.Property.FullFormat, fullFormat);
    }

}
