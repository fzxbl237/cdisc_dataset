using System;
using System.Collections.Generic;
using System.Linq;

namespace cdisc_dataset.Extensions;

public static class SearchFilterExtensions
{
    public static Func<T, bool> BuildSearchFilter<T>(string? searchText, params Func<T, string?>[] selectors)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return _ => true;

        return item => selectors.Any(selector => Contains(searchText, selector(item)));
    }

    private static bool Contains(string searchText, string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}
