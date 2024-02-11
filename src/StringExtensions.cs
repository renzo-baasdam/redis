namespace Redis;

internal static class StringExtensions
{
    internal static string AsBulkString(this string str) => $"${str.Length}\r\n{str}\r\n";
}