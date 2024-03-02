using System.Text;

namespace Redis;

internal static class StringExtensions
{
    internal static string AsArgumentString(this string str) => $"--{str}";
    internal static string AsBulkString(this string str) => $"${str.Length}\r\n{str}\r\n";

    internal static string AsBulkString(this IList<string> response)
    {
        var sb = new StringBuilder();
        sb.Append($"*{response.Count}\r\n");
        foreach (var arg in response)
        {
            sb.Append(arg.AsBulkString());
        }
        return sb.ToString();
    }

    internal static byte[] AsUtf8(this string str) => Encoding.UTF8.GetBytes(str);
    internal static string AsUtf8(this byte[] data) => Encoding.UTF8.GetString(data);
}