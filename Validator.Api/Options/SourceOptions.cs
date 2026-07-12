using System.Collections.ObjectModel;
using System.Text;

namespace P21.Validator.Api.Options;



public enum StandardStrategies
{
    NoSplit,
    GenericSplit
}

public sealed class SourceOptions : AbstractOptions
{


    private SourceOptions(Builder builder) : base(builder.Properties)
    {
        Charset = builder.Charset;
        Name = builder.Name;
        Subname = builder.Subname;
        Type = builder.Type;
        Source = builder.Source;
        MemoryStream = builder.MemoryStream;
    }

    public Encoding? Charset { get; }

    public string? Name { get; }

    public string? Subname { get; }

    public string? Source { get; }
    
    public MemoryStream? MemoryStream { get; }

    public StandardTypes Type { get; }

    public static Builder builder()
    {
        return new();
    }

    public enum StandardTypes
    {
        SasTransport,
        Delimited,
        DatasetXml
    }

    //public sealed class Extension
    //{
    //    private readonly string name;
    //    private readonly Action<Builder> modifier;

    //    Extension(string name, Action<Builder> modifier)
    //    {
    //        this.name = name;
    //        this.modifier = modifier;
    //    }
    //}

    public sealed class StandardTypeItem
    {
        public StandardTypes Type { get; }


        private readonly Dictionary<string, Action<Builder>?> extensions = new(StringComparer.OrdinalIgnoreCase);

        public StandardTypeItem(StandardTypes type)
        {
            Type = type;
            switch (type)
            {
                case StandardTypes.SasTransport:
                    extensions.Add("xpt", (b) => { });
                    break;
                case StandardTypes.DatasetXml:
                    extensions.Add("xml", (b) => { });
                    break;
                case StandardTypes.Delimited:
                    extensions.Add("dlm", (b) => { });
                    extensions.Add("csv", (b) => { b.WithProperty("Delimiter", ","); });
                    extensions.Add("tab", (b) => { b.WithProperty("Delimiter", "\t"); });
                    extensions.Add("pipe", (b) => { b.WithProperty("Delimiter", "|"); });
                    extensions.Add("dollar", (b) => { b.WithProperty("Delimiter", "$"); });
                    break;
            }
            ;
        }

        public bool TryModify(string extension, Builder builder)
        {
            this.extensions.TryGetValue(extension, out Action<Builder>? modifier);

            if (modifier is null)
            {
                return false;
            }

            modifier(builder);
            return true;
        }


    }

    public static Builder? WithType(string? type, Builder builder)
    {
        StandardTypeItem chosenType = null;

        if (!string.IsNullOrWhiteSpace(type))
        {
            type = type.ToLower();
            foreach (var item in Enum.GetNames(typeof(StandardTypes)))
            {
                Enum.TryParse<StandardTypes>(item, out StandardTypes standardTypes);
                var typeItem = new StandardTypeItem(standardTypes);
                if (typeItem.TryModify(type, builder))
                {
                    chosenType = typeItem;
                    break;
                }
            }
        }
        if (chosenType == null)
        {
            return null;
        }
        return builder.WithType(chosenType.Type);
    }

    public sealed class Builder : AbstractBuilder<Builder>
    {
        internal Encoding? Charset { get; private set; }
        internal string? Name { get; private set; }
        internal string? Subname { get; private set; }
        internal StandardTypes Type { get; private set; }
        internal string? Source { get; private set; }
        
        internal MemoryStream? MemoryStream { get; private set; }

        public Builder WithCharset(Encoding charset)
        {
            Charset = charset;
            return this;
        }
        
        public Builder WithMemoryStream(MemoryStream stream)
        {
            MemoryStream = stream;
            return this;
        }

        public Builder WithName(string name)
        {
            Name = name;
            return this;
        }

        public Builder WithSubname(string subname)
        {
            Subname = subname;
            return this;
        }

        public Builder WithType(StandardTypes type)
        {
            Type = type;
            return this;
        }



        public Builder WithSource(string source)
        {
            Source = source;
            return this;
        }

        public SourceOptions Build()
        {
            return new SourceOptions(this);
        }
    }
}
