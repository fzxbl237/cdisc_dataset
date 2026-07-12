namespace P21.Validator.Api.Options;

public sealed class ConfigOptions : AbstractOptions
{
    
    private readonly List<string> configs;
    private ConfigOptions(Builder builder) : base(builder.Properties)
    {
        this.configs = builder._configs;
    }

    public static Builder CreateBuilder()
    {
        return new();
    }

    public string? GetDefine()
    {
        return GetProperty("Define");
    }

    public IReadOnlyList<string> GetConfigs()
    {
        return configs;
        // var value = GetProperty("Configs");
        // if (string.IsNullOrWhiteSpace(value))
        // {
        //     return Array.Empty<string>();
        // }
        //
        // return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
        //     .Select(item => item.Trim())
        //     .Where(item => item.Length > 0)
        //     .ToList();
    }

    public sealed class Builder : AbstractBuilder<Builder>
    {

        public List<string> _configs;
        public ConfigOptions Build()
        {
            return new ConfigOptions(this);
        }
        

        public Builder WithConfigs(List<string> configs)
        {
            this._configs =  configs;
            return this;
        }

        //public Builder WithProperty(string key, string value)
        //{
        //    return WithProperty(key, value);
        //}
    }
}
