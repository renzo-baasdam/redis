using System.Net;
using System.Net.Sockets;

Console.WriteLine("Starting TcpListener!");
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();
while (true)
{
    // listen
    var client = server.AcceptTcpClient();
    var stream = client.GetStream();

    // input bytes to string
    var buffer = new byte[512];
    stream.Read(buffer, 0, buffer.Length);
    var input = System.Text.Encoding.UTF8.GetString(buffer);

    // output string to bytes
    var output = "+PONG\r\n";
    var outputBuffer = System.Text.Encoding.UTF8.GetBytes(output);

    // log and respond
    Console.WriteLine(@$"Received: {input.Replace("\r\n", "\\r\\n")}. Response: {output.Replace("\r\n", "\\r\\n")}");
    stream.Write(outputBuffer);
    client.Close();
}