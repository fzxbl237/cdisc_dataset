using System;
using System.Collections.Generic;
using System.Linq;

namespace cdisc_dataset.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    /// 标记列表中具有重复键的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="source">源列表</param>
    /// <param name="keySelector">键选择器</param>
    /// <param name="duplicateFlagSetter">重复标记设置器</param>
    /// <param name="keyValidator">键验证器（可选）</param>
    public static void MarkDuplicates<T, TKey>(
        this IEnumerable<T> source,
        Func<T, TKey> keySelector,
        Action<T, bool> duplicateFlagSetter,
        Func<TKey, bool>? keyValidator = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(duplicateFlagSetter);

        // 创建分组字典
        var groupedItems = source
            .Select(item => new { Item = item, Key = keySelector(item) })
            .Where(x => keyValidator == null || keyValidator(x.Key))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item).ToList());

        // 设置重复标志
        foreach (var group in groupedItems)
        {
            bool isDuplicate = group.Value.Count > 1;
            foreach (var item in group.Value)
            {
                duplicateFlagSetter(item, isDuplicate);
            }
        }
    }
}