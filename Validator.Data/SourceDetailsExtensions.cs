using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public static class SourceDetailsExtensions
{
    public static bool IsConvertible(this SourceDetails.Property property)
    {
        return property switch
        {
            SourceDetails.Property.Combined => true,
            SourceDetails.Property.Corrupted => true,
            SourceDetails.Property.FileSize => true,
            SourceDetails.Property.Filtered => true,
            SourceDetails.Property.Records => true,
            SourceDetails.Property.Split => true,
            SourceDetails.Property.Validated => true,
            SourceDetails.Property.Variables => true,
            _ => false
        };
    }
}
