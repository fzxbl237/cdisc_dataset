using P21.Validator.Api.Models;
using P21.Validator.Api.Options;
using System;
using System.Collections.Generic;
using static P21.Validator.Api.Options.SourceOptions;

namespace P21.Validator.Data;

public sealed class SourceProvider
{
    private readonly Dictionary<string, AbstractDataSource> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataEntryFactory _factory;

    private readonly Dictionary<string, DataSource> sources = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<StandardTypes, Type> SOURCE_TYPES = new Dictionary<StandardTypes, Type>
    {
        { StandardTypes.DatasetXml, typeof(DatasetXmlDataSource) },
        { StandardTypes.SasTransport, typeof(SasTransportDataSource) },
        { StandardTypes.Delimited, typeof(DelimitedDataSource) }
    };

    public SourceProvider(DataEntryFactory factory)
    {
        _factory = factory;
    }

    public void Add(SourceOptions options)
    {
        DataSource? createdSource = this.TryCreateSource(options);

        if (createdSource == null || !createdSource.Test())
        {
            // TODO: Create dummy source
            return;
        }
        if (string.IsNullOrEmpty(options.Name)) return;

        this.sources.TryGetValue(options.Name, out DataSource? existingSource);

        if (existingSource != null)
        {
            CombinedDataSource combinedSource;

            if (!existingSource.IsComposite())
            {
                existingSource.GetDetails().SetProperty(SourceDetails.Property.Split, true);
                combinedSource = new CombinedDataSource(this._factory, existingSource);

                // Replace the existing normal source with the new composite once
                this.sources.Add(options.Name, combinedSource);
            }
            else
            {
                if (!(existingSource is CombinedDataSource)) {
                    throw new Exception(
                        $"existing source with name {options.Name} was not a CombinedDataSource as expected");
                }

                combinedSource = (CombinedDataSource)existingSource;
            }

            createdSource.GetDetails().SetProperty(SourceDetails.Property.Split, true);
            combinedSource.Add(createdSource);
        }
        else
        {
            this.sources.Add(options.Name, createdSource);
        }
    }

    public void Add(List<SourceOptions> sources)
    {
        foreach (var source in sources)
        {
            this.Add(source);
        }
    }

    private DataSource? TryCreateSource(SourceOptions options)
    {
        SOURCE_TYPES.TryGetValue(options.Type, out Type? type);
        if (type != null)
        {
            object? impl = Activator.CreateInstance(type, [options, this._factory]);
            if (impl is DataSource dataSource)
            {
                return dataSource;
            }
        }
        return null;
    }

    public DataSource? GetSource(string sourceName)
    {
        return this.GetSource(sourceName, false);
    }

    public DataSource? GetSource(String sourceName, bool replicated)
    {
        if (!this.sources.ContainsKey(sourceName))
        {
            throw new ArgumentException($"Unknown source {sourceName}");
        }

        this.sources.TryGetValue(sourceName, out DataSource? source);

        if (source != null && replicated)
        {
            try
            {
                source = source.Replicate();
            }
            catch (InvalidDataException e) { }
        }

        return source;
    }

    public HashSet<String>? GetSourceNames()
    {
        var keys = this.sources.Keys.ToHashSet();
        return keys;
    }

    public void AddSource(string name, AbstractDataSource source) => _sources[name] = source;


    public bool ContainsSource(String sourceName)
    {
        return this.sources.ContainsKey(sourceName);
    }

    public bool ContainsValidSource(String sourceName)
    {
        return this.ContainsSource(sourceName);
    }


    // TODO: Method should maybe be on SourceOptions itself but it requires some knowledge of what sources we can create?
    public SourceOptions ParseSource(String connectionString)
    {
        SourceOptions.Builder builder = null;

        bool headerless = false;
        bool unquoted = false;
        bool isLegacy = false;
        string? type = string.Empty;

        // TODO: Remove legacy support
        if (connectionString.StartsWith("FILE:"))
        {
            string[] pieces = connectionString.Split(":", 3);

            if (pieces.Length == 3)
            {
                isLegacy = true;

                string typeName = pieces[1];
                string location = pieces[2];

                builder = SourceOptions.builder()
                        .WithSource(location);

                pieces = typeName.Split("-");
                type = pieces[0];

                for (int i = 1; i < pieces.Length; ++i)
                {
                    if ("HEADERLESS".Equals(pieces[i],StringComparison.CurrentCultureIgnoreCase))
                    {
                        headerless = true;
                    }
                    else if ("UNQUOTED".Equals(pieces[i], StringComparison.CurrentCultureIgnoreCase))
                    {
                        unquoted = true;
                    }
                }
            }
        }

        if (!isLegacy)
        {
            try
            {
                Uri uri = new Uri(connectionString);
                Dictionary<string,string> _params = ParseSimpleQueryString(uri);

                builder = SourceOptions.builder()
                        .WithSource(uri.AbsolutePath);

                 _params.Remove("type",out type);

                if (string.IsNullOrEmpty(type) && "file".Equals(uri.Scheme,StringComparison.CurrentCultureIgnoreCase))
                {
                    type = new FileInfo(uri.LocalPath).Extension;
                }

                headerless = _params.Remove("headerless");
                unquoted = _params.Remove("unquoted");
            }
            catch (UriFormatException e)
            {
                Console.WriteLine($"Unable to parse {connectionString} as a valid URI to create source options");
            }
        }

        if (builder == null)
        {
            return null;
        }

        string? chosenType = type;
        if(SourceOptions.WithType(type, builder) is null)
        {
            throw new Exception($"{connectionString} has unknown source type {chosenType}");
        }


        if (headerless)
        {
            builder.WithProperty("Header", "false");
        }

        if (unquoted)
        {
            builder.WithProperty("Qualifier", "\0");
        }

        return builder.WithName(connectionString)
                .Build();
    }

    // This isn't a universally correct method of doing this but our use case is pretty limited
    private static Dictionary<string, string> ParseSimpleQueryString(Uri uri)
    {

        if (uri == null || string.IsNullOrEmpty(uri.Query))
            return new Dictionary<string, string>();

        var query = uri.Query;
        if (query.StartsWith("?"))
            query = query.Substring(1);

        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(param => param.Split('=', 2))
            .Select(parts => new
            {
                Key = parts.Length > 0 ? Uri.UnescapeDataString(parts[0]) : "",
                Value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : ""
            })
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .ToDictionary(
                kv => kv.Key.ToLowerInvariant(),
                kv => kv.Value
            );
    }

    

}
