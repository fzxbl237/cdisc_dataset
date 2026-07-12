namespace cdisc_dataset.Extensions;

public static class DataEntryExtension
{
    public static int GetDecimalPlaces(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // 去除首尾空格
        text = text.Trim();

        // 查找小数点位置
        int dotIndex = text.IndexOf('.');
        if (dotIndex == -1)
            return 0; // 没有小数点

        // 获取小数点后的部分
        string decimalPart = text.Substring(dotIndex + 1);
    
        // 统计有效数字（去掉末尾的0）
        return decimalPart.TrimEnd('0').Length;
    }
}