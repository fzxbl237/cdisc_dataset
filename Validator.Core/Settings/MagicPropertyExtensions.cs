namespace P21.Validator.Core.Settings;

internal static class MagicPropertyExtensions
{
    public static string GetSingular(this MagicVariableParser.MagicProperty property)
    {
        return property switch
        {
            MagicVariableParser.MagicProperty.Variables => "Variable",
            MagicVariableParser.MagicProperty.Domains => "Domain",
            _ => property.ToString()
        };
    }

    public static IReadOnlyList<string> GetProperties(this MagicVariableParser.MagicProperty property)
    {
        return property switch
        {
            MagicVariableParser.MagicProperty.Variables => ["If","Terms","Test","Variable","When","GroupBy"],
            MagicVariableParser.MagicProperty.Domains => new[] { "From", "Terms" },
            _ => Array.Empty<string>()
        };
    }
}
