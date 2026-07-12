namespace Net.Pinnacle21.Define.Parser;

public sealed class WhereClauseParserOptions
{
    private WhereClauseParserOptions(Builder builder)
    {
        AllowWhere = builder.AllowWhere;
        AllowOr = builder.AllowOr;
        AllowOperatorSymbols = builder.AllowOperatorSymbols;
        AllowConjunctionSymbols = builder.AllowConjunctionSymbols;
        AllowBrackets = builder.AllowBrackets;
        AllowNullChecks = builder.AllowNullChecks;
        AllowComplexNames = builder.AllowComplexNames;
    }

    public bool AllowWhere { get; }
    public bool AllowOr { get; }
    public bool AllowOperatorSymbols { get; }
    public bool AllowConjunctionSymbols { get; }
    public bool AllowBrackets { get; }
    public bool AllowNullChecks { get; }
    public bool AllowComplexNames { get; }

    public static Builder CreateBuilder() => new();

    public sealed class Builder
    {
        public bool AllowWhere { get; private set; } = true;
        public bool AllowOr { get; private set; } = true;
        public bool AllowOperatorSymbols { get; private set; } = true;
        public bool AllowConjunctionSymbols { get; private set; } = true;
        public bool AllowBrackets { get; private set; } = true;
        public bool AllowNullChecks { get; private set; }
        public bool AllowComplexNames { get; private set; }

        public Builder SetWhereAllowed(bool allow) { AllowWhere = allow; return this; }
        public Builder SetOrAllowed(bool allow) { AllowOr = allow; return this; }
        public Builder SetOperatorSymbolsAllowed(bool allow) { AllowOperatorSymbols = allow; return this; }
        public Builder SetConjunctionSymbolsAllowed(bool allow) { AllowConjunctionSymbols = allow; return this; }
        public Builder SetBracketsAllowed(bool allow) { AllowBrackets = allow; return this; }
        public Builder SetNullChecksAllowed(bool allow) { AllowNullChecks = allow; return this; }
        public Builder SetComplexNamesAllowed(bool allow) { AllowComplexNames = allow; return this; }

        public WhereClauseParserOptions Build() => new(this);
    }
}
