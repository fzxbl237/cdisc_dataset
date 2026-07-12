using System.Reflection;
using System.Text;

namespace P21.Validator.Core.Util;

public static class Helpers
{
    public static string[] TrimSplit(string value, string delimiter)
    {
        var split = value.Split(delimiter, StringSplitOptions.None);
        for (var i = 0; i < split.Length; ++i)
        {
            split[i] = split[i].Trim();
        }

        return split;
    }

    public static int[] DetermineLargestIncreasingSubsequence(int[] input)
    {
        return DetermineLargestIncreasingSubsequence(input, false);
    }

    public static int[] DetermineLargestIncreasingSubsequence(int[] input, bool returnIndexes)
    {
        var length = 0;
        var count = input.Length + 1;
        var current = new int[count];
        var previous = new int[count];

        Array.Copy(input, 0, current, 1, input.Length);
        input = current;
        current = new int[count];

        for (var i = 1; i < count; ++i)
        {
            int j;
            if (length == 0 || input[current[1]] >= input[i])
            {
                j = 0;
            }
            else
            {
                var low = 1;
                var high = length + 1;

                while (low < high - 1)
                {
                    var mid = (low + high) / 2;
                    if (input[current[mid]] < input[i])
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                j = low;
            }

            previous[i] = current[j];

            if (j == length || input[i] < input[current[j + 1]])
            {
                current[j + 1] = i;
                length = Math.Max(length, j + 1);
            }
        }

        var subsequence = new int[length];
        var position = current[length];

        while (length > 0)
        {
            subsequence[length - 1] = returnIndexes ? position - 1 : input[position];
            position = previous[position];
            length--;
        }

        return subsequence;
    }

    public static List<Type> Find(Type definition)
    {
        return Find(definition, true);
    }

    public static List<Type> Find(Type definition, bool useCache)
    {
        var implementations = new List<Type>();
        var loader = Assembly.GetExecutingAssembly();
        var servicesPrefix = "META-INF/services/" + definition.FullName;

        foreach (var resource in loader.GetManifestResourceNames())
        {
            if (!resource.EndsWith(servicesPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = loader.GetManifestResourceStream(resource);
            if (stream == null)
            {
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    continue;
                }

                var comment = line.IndexOf('#');
                if (comment == 0)
                {
                    continue;
                }

                if (comment > 0)
                {
                    line = line[..comment];
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var implementation = Type.GetType(line, false);
                if (implementation != null && definition.IsAssignableFrom(implementation))
                {
                    implementations.Add(implementation);
                }
            }
        }

        return implementations;
    }
}
