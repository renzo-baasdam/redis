using System.Text;

namespace Redis;

internal static class StringExtensions
{
    internal static string AsBulkString(this string str) => $"${str.Length}\r\n{str}\r\n";

    internal static string AsBulkString(this string[] response)
    {
        var sb = new StringBuilder();
        sb.Append($"*{response.Length}\r\n");
        foreach (var arg in response)
        {
            sb.Append(arg.AsBulkString());
        }
        return sb.ToString();
    }
}