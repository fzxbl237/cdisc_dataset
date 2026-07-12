namespace P21.Validator.Api.Options;

public sealed class ValidationOptions : AbstractOptions
{
    private ValidationOptions(Builder builder) : base(builder.Properties)
    {
    }

    public static Builder CreateBuilder()
    {
        return new();
    }

    public sealed class Builder : AbstractBuilder<Builder>
    {
        public ValidationOptions Build()
        {
            return new ValidationOptions(this);
        }

        //public Builder WithProperty(string key, string value)
        //{
        //    return Set(key, value);
        //}
    }
}
