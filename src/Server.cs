using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting TcpListener");
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();
var socket = server.AcceptSocket();
Console.WriteLine("Accepted Socket");
while (true)
{
    // receive
    var buffer = new byte[512];
    socket.Receive(buffer);

    // input bytes to string
    var input = Encoding.UTF8.GetString(buffer);

    // output string to bytes
    var output = "+PONG\r\n";
    var outputBuffer = Encoding.UTF8.GetBytes(output);

    // log and respond
    Console.WriteLine(@$"Received: {input.Replace("\r\n", "\\r\\n")}. Response: {output.Replace("\r\n", "\\r\\n")}");
    socket.Send(outputBuffer);
}