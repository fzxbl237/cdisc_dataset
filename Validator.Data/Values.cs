namespace P21.Validator.Data;

public static class Values
{
    public static double Round(double input, int cutoff)
    {
        var truncated = (long)input;
        var remainder = input - truncated;

        if (remainder == 0)
        {
            return input;
        }

        var magnitude = Math.Pow(10, cutoff - (int)Math.Ceiling(Math.Log10(remainder < 0 ? -remainder : remainder)));
        return truncated + Math.Round(remainder * magnitude) / magnitude;
    }
}
