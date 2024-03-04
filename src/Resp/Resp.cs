using System.Text;

namespace Redis;

public class Message
{
    public string Original { get; set; } = string.Empty;
}
public class Command : Message
{
    public string[] Arguments { get; set; } = Array.Empty<string>();
    public string this[int i] => Arguments[i];
    public int Length => Arguments.Length;
}
public class Response : Message { }

public static class Resp
{
    public static List<Message> Parse(string input)
    {
        Console.WriteLine($"Parsing {input}");
        var messages = new List<Message>();
        var lines = input.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\n'))
            .Where(line => line != string.Empty)
            .ToArray();
        int index = 0;
        while (index < lines.Length)
        {
            if (lines[index][0] == '\n') lines[index] = lines[index][1..];
            if (lines[index][0] == '*')
            {
                if (!int.TryParse(lines[index][1..], out int arguments) || index + 2 * arguments > lines.Length)
                {
                    Console.WriteLine("Invalid command");
                    break;
                }
                var message = new Command { 
                    Arguments = new string[arguments], 
                    Original = string.Join("\r\n", lines[index..(index + arguments * 2 + 1)]) + "\r\n",
                };
                for (int j = 0; j < arguments; ++j)
                {
                    message.Arguments[j] = lines[index + (j + 1) * 2];
                }
                index = index + 2 * arguments + 1;
                messages.Add(message);
            }
            else if (lines[index][0] == '+')
            {
                var message = new Response { Original = lines[index] + "\r\n" };
                ++index;
                messages.Add(message);
            }
            else if (lines[index][0] == '$')
            {
                if(!int.TryParse(lines[index][1..], out int length))
                {
                    ++index;
                    continue;
                }
                if(length == -1)
                {
                    messages.Add(new Response { Original = lines[index++] + "\r\n" });
                    continue;
                }
                var sb = new StringBuilder();
                sb.Append(lines[index] + "\r\n");
                int headerLength = sb.Length;
                ++index;
                while(sb.Length - headerLength < length && index < lines.Length)
                {
                    sb.Append(lines[index++] + "\r\n");
                }
                var message = new Response { Original = sb.ToString() };
                messages.Add(message);
            }
            else
            {
                var message = new Response { Original = input };
                ++index;
                messages.Add(message);
            }
        }
        return messages;
    }
}