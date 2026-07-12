using System.Security.Cryptography;
using System.Text;

namespace P21.Validator.Data;

public static class Hex
{
    private static readonly char[] HexTable =
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
    };

    public static string ToString(byte[] bytes)
    {
        var render = new char[bytes.Length * 2];
        for (var i = 0; i < render.Length; i += 2)
        {
            var b = bytes[i / 2];
            render[i] = HexTable[(b >> 4) & 0x0F];
            render[i + 1] = HexTable[b & 0x0F];
        }

        return new string(render);
    }

    public static string Sha1(string value)
    {
        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
        return ToString(bytes);
    }
}
