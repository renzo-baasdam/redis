using System.Text;

namespace Redis.Extensions;

internal static class StringExtensions
{
    internal static string AsArgumentString(this string str) => $"--{str}";
    internal static string AsSimpleString(this string str) => $"+{str}\r\n";
    internal static string AsBulkString(this string str) => $"${str.Length}\r\n{str}\r\n";
    internal static byte[] AsBulkString(this byte[] bytes)
    {
        var prefix = $"${bytes.Length}\r\n".AsUtf8();
        var postfix = "\r\n".AsUtf8();
        return prefix.Concat(bytes).Concat(postfix).ToArray();
    }
    internal static string AsArrayString(this IList<MessageV2> values)
    {
        var sb = new StringBuilder();
        sb.Append($"*{values.Count}\r\n");
        foreach (var arg in values)
        {
            sb.Append(arg);
        }
        return sb.ToString();
    }
    internal static string AsArrayString(this IList<string> response)
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