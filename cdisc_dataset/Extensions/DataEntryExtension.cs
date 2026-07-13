using System;
using System.Collections.Generic;
using System.Linq;
using P21.Validator.Api.Options;
using P21.Validator.Data;

namespace cdisc_dataset.Extensions;

public static class DataEntryExtension
{
    public static int? GetDecimalPlaces(this List<DataRecord> allRecords, string variableName)
    {
        // 获取所有记录的 DataEntry
        var entries = allRecords
            .Select(o => o.GetValue(variableName))
            .ToList();

        if (entries.Count == 0)
        {
            return null;
        }

        // 找出 Type 的最小值
        return entries.Max(e =>
        {
            var s = e.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            var trim = s.Trim();
            int dotIndex = trim.IndexOf('.');
            if (dotIndex == -1)
                return null; // 没有小数点

            // 获取小数点后的部分
            string decimalPart = trim.Substring(dotIndex + 1);

            // 统计有效数字（去掉末尾的0）
            return decimalPart.TrimEnd('0').Length;
        });
        
    }

    public static string? InferDataType(this List<DataRecord> allRecords, string variableName)
    {
        // 如果变量名以 DTC 结尾，返回 datetime
        if (variableName.EndsWith("DTC", StringComparison.OrdinalIgnoreCase))
        {
            return "datetime";
        }

        // 获取所有记录的 DataEntry
        var entries = allRecords
            .Select(o => o.GetValue(variableName))
            .ToList();

        if (entries.Count == 0)
        {
            return null;
        }

        var dataEntryFactory = new DataEntryFactory(ValidationOptions.CreateBuilder().Build());
        // 找出 Type 的最小值
        var minType = entries.Min(o=>dataEntryFactory.Create(o.ToString()).Type);

        // 转换为字符串返回
        return minType.ToString().ToLowerInvariant();
    }

    public static string? InferOrigin(this string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        // 转换为标准格式用于比较
        var upperName = variableName.ToUpperInvariant();

        // DD0105: Study Day 变量 (--DY, --STDY, --ENDY) 必须设置为 Derived
        if (upperName.EndsWith("DY") || upperName.EndsWith("STDY") || upperName.EndsWith("ENDY"))
        {
            return "Derived";
        }

        // DD0106: DOMAIN 变量应该设置为 Assigned
        if (upperName == "DOMAIN")
        {
            return "Assigned";
        }

        // DD0107: RDOMAIN 变量应该设置为 Assigned
        if (upperName == "RDOMAIN")
        {
            return "Assigned";
        }

        // DD0108: STUDYID 变量应该设置为 Protocol
        if (upperName == "STUDYID")
        {
            return "Protocol";
        }

        // 其他变量返回 null
        return null;
    }

    public static string InferCodeListOid(this List<string?>? values)
    {
        if (values == null || values.Count == 0)
        {
            return "CL.YN";
        }

        // 转换为 HashSet 进行集合操作，忽略大小写
        var inputSet = new HashSet<string>(values.Select(v => v?.ToUpperInvariant() ?? string.Empty));
        
        // 定义基准集合
        var nySet = new HashSet<string> { "Y", "N" };
        var nynaSet = new HashSet<string> { "Y", "N", "NA" };
        var nyuSet = new HashSet<string> { "Y", "N", "U" };

        // 检查是否是 {"Y", "N"} 的子集
        if (inputSet.IsSubsetOf(nySet))
        {
            return "CL.NYO";
        }

        // 检查是否是 {"Y", "N", "NA"} 的子集
        if (inputSet.IsSubsetOf(nynaSet))
        {
            return "CL.NYNA";
        }

        // 检查是否是 {"Y", "N", "U"} 的子集
        if (inputSet.IsSubsetOf(nyuSet))
        {
            return "CL.NYU";
        }

        // 其他情况
        return "CL.YN";
    }


}