using System.Globalization;
using System.Text.RegularExpressions;
using P21.Validator.Api.Options;

namespace P21.Validator.Data;

public sealed class DataEntryFactory
{
    private static readonly Regex DatePattern = new(
        "^(?:-|[0-9]{4})(?:-(?:-|0[0-9]|1[0-2])(?:-(?:-|[0-2][0-9]|3[0-1])(?:T(?:-|[0-1][0-9]|2[0-4])(?::(?:-|[0-5][0-9])(?::[0-5][0-9])?)?)?)?)?$",
        RegexOptions.Compiled
    );

    private readonly int _decimalCount;

    public DataEntryFactory(ValidationOptions options)
    {
        var decimalCount = 8;
        if (options.HasProperty("Engine.RoundingDigits"))
        {
            if (int.TryParse(options.GetProperty("Engine.RoundingDigits"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                decimalCount = parsed;
            }
        }

        _decimalCount = decimalCount;
    }

    public int DecimalCount => _decimalCount;

    public DataEntry Create(object? value)
    {
        if (value is string stringValue)
        {
            value = RTrim(stringValue);
        }

        if (value is null)
        {
            return DataEntry.NullEntry;
        }

        return new DataEntryImpl(value, _decimalCount);
    }

    private sealed class DataEntryImpl : DataEntry
    {
        private readonly object? _value;
        private readonly DataEntry.DataType _type;

        public DataEntryImpl(object value, int decimalCount)
        {
            var comparisonDigits = decimalCount - 2;

            if (value is decimal decimalValue)
            {
                _type = DataType.Float;
                _value = Math.Round(decimalValue, comparisonDigits, MidpointRounding.AwayFromZero);
                return;
            }

            _type = DetermineImplicitType(value);

            if (_type is DataType.Float or DataType.Integer)
            {
                if (value is float floatValue)
                {
                    value = (double)floatValue;
                }

                if (value is double doubleValue)
                {
                    if (Math.Abs(doubleValue - Math.Round(doubleValue)) < double.Epsilon)
                    {
                        value = (long)Math.Round(doubleValue);
                    }
                }

                decimal converted;
                if (value is string stringNumeric)
                {
                    converted = decimal.Parse(stringNumeric, CultureInfo.InvariantCulture);
                }
                else if (value is double doubleNumeric)
                {
                    converted = decimal.Parse(doubleNumeric.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                }
                else if (value is long longValue)
                {
                    converted = longValue;
                }
                else if (value is int intValue)
                {
                    converted = intValue;
                }
                else
                {
                    throw new ArgumentException($"Unable to handle provided value of type {value.GetType().Name}");
                }

                converted = Math.Round(converted, comparisonDigits, MidpointRounding.AwayFromZero);
                value = new NumericValue(converted, value.ToString() ?? string.Empty);
            }

            _value = value;
        }

        public override int CompareToAny(object? other, bool caseSensitive)
        {
            var rhsEntry = other as DataEntry ?? new DataEntryImpl(other ?? string.Empty, 8);
            var lhs = GetValue();
            var rhs = rhsEntry.GetValue();
            var lhsType = Type;
            var rhsType = rhsEntry.Type;

            if (rhs is null)
            {
                return 1;
            }

            if (lhsType is DataType.Float or DataType.Integer && rhsType is DataType.Float or DataType.Integer)
            {
                return ((decimal)lhs!).CompareTo((decimal)rhs!);
            }

            var lhsText = lhs?.ToString() ?? string.Empty;
            var rhsText = rhs?.ToString() ?? string.Empty;
            var lhsDate = lhsType == DataType.DateTime || (lhsType == DataType.Integer && lhsText.Length == 4);
            var rhsDate = rhsType == DataType.DateTime || (rhsType == DataType.Integer && rhsText.Length == 4);

            if (lhsDate && rhsDate && (lhsText.StartsWith(rhsText, StringComparison.Ordinal) || rhsText.StartsWith(lhsText, StringComparison.Ordinal)))
            {
                return 0;
            }

            if (!caseSensitive)
            {
                lhsText = lhsText.ToUpperInvariant();
                rhsText = rhsText.ToUpperInvariant();
            }

            return string.Compare(lhsText, rhsText, StringComparison.Ordinal);
        }

        public override int CompareTo(DataEntry? other)
        {
            if (other is null)
            {
                return 1;
            }

            var lhsSort = GetSortOrder(Type);
            var rhsSort = GetSortOrder(other.Type);

            if (lhsSort != rhsSort)
            {
                return lhsSort - rhsSort;
            }

            if (Type is DataType.Float or DataType.Integer)
            {
                return ((decimal)GetValue()!).CompareTo((decimal)other.GetValue()!);
            }

            return string.Compare(GetValue()?.ToString(), other.GetValue()?.ToString(), StringComparison.Ordinal);
        }

        public override DataEntry.DataType Type => _type;

        public override object? GetValue()
        {
            return _value is NumericValue numeric ? numeric.Value : _value;
        }

        public override bool IsNumeric => _type is DataType.Float or DataType.Integer;

        public override bool IsDate => _type is DataType.Date or DataType.DateTime;

        public override bool HasValue => _value is not null;

        public override bool Equals(object? obj)
        {
            return obj is DataEntry entry && CompareTo(entry) == 0;
        }

        public override int GetHashCode()
        {
            return HasValue ? GetValue()!.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return _value?.ToString() ?? string.Empty;
        }
    }

    private sealed class NumericValue
    {
        public NumericValue(decimal value, string text)
        {
            Value = value;
            Text = text;
        }

        public decimal Value { get; }
        public string Text { get; }
        public override string ToString() => Text;
    }

    internal sealed class NullDataEntry : DataEntry
    {
        public override int CompareToAny(object? other, bool caseSensitive)
        {
            if (other is DataEntry entry)
            {
                other = entry.GetValue();
            }

            return other is null ? 0 : -1;
        }

        public override int CompareTo(DataEntry? other)
        {
            return other?.HasValue == true ? -1 : 0;
        }

        public override DataEntry.DataType Type => DataEntry.DataType.Null;

        public override object? GetValue() => null;

        public override bool IsNumeric => false;

        public override bool IsDate => false;

        public override bool HasValue => false;

        public override string ToString() => string.Empty;
    }

    private static DataEntry.DataType DetermineImplicitType(object? value)
    {
        if (value is null)
        {
            return DataEntry.DataType.Null;
        }

        if (value is string text)
        {
            if (text.Length == 0)
            {
                return DataEntry.DataType.Text;
            }

            var firstChar = text[0];
            if (!((firstChar is '-' or '+') && text.Length > 1) && !(firstChar == '.' && text.Length > 1) && !(firstChar >= '0' && firstChar <= '9'))
            {
                return DataEntry.DataType.Text;
            }

            var couldBeDouble = firstChar == '.';
            var determinedType = true;
            var possibleNumber = firstChar != '0' || text.Length == 1 || text[1] == '.';

            if (possibleNumber)
            {
                for (var i = 1; i < text.Length; ++i)
                {
                    var currentChar = text[i];
                    if (currentChar < '0' || currentChar > '9')
                    {
                        if (currentChar == '.' && !couldBeDouble && i < text.Length - 1)
                        {
                            couldBeDouble = true;
                        }
                        else
                        {
                            determinedType = false;
                            break;
                        }
                    }
                }
            }
            else
            {
                determinedType = false;
            }

            if (determinedType)
            {
                return couldBeDouble ? DataEntry.DataType.Float : DataEntry.DataType.Integer;
            }

            return DatePattern.IsMatch(text) ? DataEntry.DataType.DateTime : DataEntry.DataType.Text;
        }

        return value switch
        {
            int or long => DataEntry.DataType.Integer,
            float or double or decimal => DataEntry.DataType.Float,
            _ => DataEntry.DataType.Text
        };
    }

    private static string? RTrim(string stringValue)
    {
        var length = stringValue.Length;
        var index = length - 1;

        for (; index > -1; --index)
        {
            if (stringValue[index] != ' ')
            {
                break;
            }
        }

        if (index == -1)
        {
            return null;
        }

        return index < length - 1 ? stringValue[..(index + 1)] : stringValue;
    }

    private static int GetSortOrder(DataEntry.DataType type)
    {
        if (type == DataEntry.DataType.Null)
        {
            return 1;
        }

        if (type is DataEntry.DataType.Float or DataEntry.DataType.Integer)
        {
            return 2;
        }

        if (type is DataEntry.DataType.Date or DataEntry.DataType.DateTime)
        {
            return 3;
        }

        return 4;
    }
}
