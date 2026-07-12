namespace Net.Pinnacle21.Define.Parser;

public sealed class PredecessorCondition
{
    private PredecessorCondition(string? datasetName, string variableName, string value)
    {
        DatasetName = datasetName;
        VariableName = variableName;
        Value = value;
    }

    public string? DatasetName { get; }
    public string VariableName { get; }
    public string Value { get; }

    public static PredecessorCondition Of(string variableName, string value) => Of(null, variableName, value);

    public static PredecessorCondition Of(string? datasetName, string variableName, string value)
    {
        if (string.IsNullOrWhiteSpace(variableName)) throw new ArgumentException("The predecessor variable name cannot be blank.");
        return new PredecessorCondition(datasetName, variableName, value);
    }

    public string AsText() => DatasetName is null ? $"{VariableName} = '{Value.Replace("'", "''")}'" : $"{DatasetName}.{VariableName} = '{Value.Replace("'", "''")}'";
    public override string ToString() => AsText();
}

public sealed class Predecessor
{
    private Predecessor(Builder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.DatasetName)) throw new InvalidOperationException("The predecessor dataset name cannot be null.");
        if (string.IsNullOrWhiteSpace(builder.VariableName)) throw new InvalidOperationException("The predecessor variable name cannot be null.");
        DatasetName = builder.DatasetName!;
        VariableName = builder.VariableName!;
        Conditions = builder.Conditions.ToList().AsReadOnly();
    }

    public string DatasetName { get; }
    public string VariableName { get; }
    public IReadOnlyCollection<PredecessorCondition> Conditions { get; }
    public string AsText() => Conditions.Count == 0 ? $"{DatasetName}.{VariableName}" : $"{DatasetName}.{VariableName} WHERE {string.Join(" AND ", Conditions.Select(c => c.AsText()))}";
    public override string ToString() => AsText();
    public static Builder CreateBuilder() => new();

    public sealed class Builder
    {
        public string? DatasetName { get; private set; }
        public string? VariableName { get; private set; }
        public List<PredecessorCondition> Conditions { get; } = [];
        public Builder SetDatasetName(string? datasetName) { DatasetName = datasetName; return this; }
        public Builder SetVariableName(string? variableName) { VariableName = variableName; return this; }
        public Builder AddCondition(PredecessorCondition condition) { Conditions.Add(condition ?? throw new ArgumentNullException(nameof(condition))); return this; }
        public Builder AddConditions(IEnumerable<PredecessorCondition> conditions) { foreach (var condition in conditions) AddCondition(condition); return this; }
        public Predecessor Build() => new(this);
    }
}

public sealed class PredecessorParser
{
    public static PredecessorParser Create() => new();

    public Predecessor? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Trim().Split(new[] { " where ", " WHERE " }, StringSplitOptions.None);
        var main = parts[0].Trim();
        var dot = main.IndexOf('.');
        if (dot <= 0 || dot == main.Length - 1) return null;
        var builder = Predecessor.CreateBuilder().SetDatasetName(main[..dot].Trim()).SetVariableName(main[(dot + 1)..].Trim());
        if (parts.Length > 1)
        {
            foreach (var c in parts[1].Split(new[] { " and ", " AND " }, StringSplitOptions.None))
            {
                var m = System.Text.RegularExpressions.Regex.Match(c.Trim(), @"^(?:(?<ds>[\w.]+)\.)?(?<var>[\w.]+)\s*=\s*(?<val>.*)$");
                if (!m.Success) return null;
                var ds = m.Groups["ds"].Value;
                var variable = m.Groups["var"].Value;
                var val = m.Groups["val"].Value.Trim();
                if ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\''))) val = val[1..^1].Replace("''", "'").Replace("\"\"", "\"");
                builder.AddCondition(PredecessorCondition.Of(string.IsNullOrWhiteSpace(ds) ? null : ds, variable, val));
            }
        }
        return builder.Build();
    }
}
