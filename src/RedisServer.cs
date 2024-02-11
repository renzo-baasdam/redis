using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Redis;

internal record RedisValue
{
    public string Value { get; init; } = string.Empty;
    public DateTime? Expiration { get; init; }
}

public class RedisServer
{
    private readonly Dictionary<string, RedisValue> _dictionary = new();
    private readonly TcpListener _server = new(IPAddress.Any, 6379);

    public async Task Start()
    {
        Console.WriteLine("Starting TcpListener");
        _server.Start();
        int socketNumber = 0;
        while (true)
        {
            var socket = await _server.AcceptSocketAsync();
            Console.WriteLine($"Accepted Socket #{socketNumber}");
            Listen(socket, socketNumber);
            ++socketNumber;
        }
    }

    async void Listen(Socket socket, int socketNumber)
    {
        while (true)
        {
            try
            {
                // receive
                var buffer = new byte[512];
                await socket.ReceiveAsync(buffer, SocketFlags.None);

                // input bytes to string
                var bufferEnd = Array.IndexOf(buffer, (byte)0);
                var input = Encoding.UTF8.GetString(buffer, 0, bufferEnd);

                // output string to bytes
                var output = Response(input);
                var outputBuffer = Encoding.UTF8.GetBytes(output);

                // log and respond
                Console.WriteLine(@$"Socket #{socketNumber}. Received: {input.ReplaceLineEndings("\\r\\n")}. Response: {output.Replace("\r\n", "\\r\\n")}");
                await socket.SendAsync(outputBuffer, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    private string Get(string key)
    {
        if (_dictionary.TryGetValue(key, out var value) && (value.Expiration is not { } expiration || expiration > DateTime.Now))
            return value.Value.AsBulkString();
        return "_\r\n";
    }

    private string Set(string[] lines)
    {
        var key = lines[4];
        var value = lines[6];
        if (lines.Length > 8 && lines[8].ToUpperInvariant() == "PX")
        {
            _dictionary[key] = new RedisValue()
            {
                Value = value,
                Expiration = DateTime.Now.AddMilliseconds(int.Parse(lines[10]))
            };
        }
        else
        {
            _dictionary[key] = new RedisValue() { Value = value };
        }
        return "+OK\r\n";
    }

    private string Response(string input)
    {
        var lines = input.Split("\r\n");
        if (lines.Length > 0)
        {
            var arguments = lines[0];
            if (arguments.Length > 1
                && arguments[0] == '*'
                && int.TryParse(arguments[1..], out var numberOfArguments))
            {
                var command = lines[2];
                return command.ToUpperInvariant() switch
                {
                    "SET" => Set(lines),
                    "GET" => Get(lines[4]),
                    "ECHO" => lines[4].AsBulkString(),
                    "PING" => "+PONG\r\n",
                    _ => "Unsupported request"
                };
            }
        }
        return "Unsupported request";
    }

}