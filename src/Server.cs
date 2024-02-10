using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting TcpListener");
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();

int socketNumber = 0;
while (true)
{
    var socket = await server.AcceptSocketAsync();
    Console.WriteLine($"Accepted Socket #{socketNumber}");
    Listen(socket, socketNumber);
    ++socketNumber;
}

static async void Listen(Socket socket, int socketNumber)
{
    while (true)
    {
        try
        {
            // receive
            var buffer = new byte[512];
            await socket.ReceiveAsync(buffer, SocketFlags.None);

            // input bytes to string
            var input = Encoding.UTF8.GetString(buffer);

            // output string to bytes
            var output = "+PONG\r\n";
            var outputBuffer = Encoding.UTF8.GetBytes(output);

            // log and respond
            Console.WriteLine(@$"Socket #{socketNumber}. Received: {input.ReplaceLineEndings("\\r\\n")}. Response: {output.Replace("\r\n", "\\r\\n")}");
            await socket.SendAsync(outputBuffer, SocketFlags.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught exception: {ex}");
            break;
        }
    }
}