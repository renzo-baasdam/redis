using System.Text;

namespace Redis;

public class RespParser
{
    private readonly Stream _stream;
    private readonly Queue<MessageV2> _bufferedMessages = new();

    public RespParser(Stream stream)
    {
        _stream = stream;
    }

    public async Task<MessageV2?> ReadMessage()
    {
        if (_bufferedMessages.Any()) return _bufferedMessages.Dequeue();

        var buffer = new byte[1024];
        await _stream.ReadAsync(buffer);
        int offset = 0;
        var bufferLastIndex = Array.IndexOf(buffer, (byte)0) - 1;
        while (offset < bufferLastIndex)
        {
            var header = (char)(buffer[offset]);
            (MessageV2 msg, offset) = header switch
            {
                '+' => ParseSimpleString(buffer, bufferLastIndex, offset),
                '$' => ParseBulkString(buffer, bufferLastIndex, offset),
                '*' => ParseSimpleString(buffer, bufferLastIndex, offset),
                _ => ParseSimpleString(buffer, bufferLastIndex, offset)
            };
            _bufferedMessages.Enqueue(msg);
        }

        return _bufferedMessages.Any()
            ? _bufferedMessages.Dequeue()
            : null;
    }

    public (MessageV2, int) ParseSimpleString(byte[] buffer, int bufferLastIndex, int offset)
    {
        int end = offset + 1;
        while (end < bufferLastIndex)
        {
            if ((char)buffer[end] == '\r' && (char)buffer[end + 1] == '\n') break;
            ++end;
        }
        if (end == bufferLastIndex) throw new InvalidOperationException($"Reached end of buffer before finding an \\r\\n.");
        var value = Encoding.UTF8.GetString(buffer, offset + 1, end - offset - 1);
        return (new SimpleStringMessage(value), end + 2);
    }

    public (MessageV2, int) ParseBulkString(byte[] buffer, int bufferLastIndex, int offset)
    {
        int length = 0;
        int end = offset + 1;
        while (end < bufferLastIndex)
        {
            if ((char)buffer[end] == '\r' && (char)buffer[end + 1] == '\n') break;
            int num = buffer[end] - '0';
            if (num >= 0 && num <= 9) length = length * 10 + num;
            else throw new InvalidOperationException($"Bulk string didn't start with a number.");
            ++end;
        }
        if (end == bufferLastIndex) throw new InvalidOperationException($"Reached end of buffer before finding an \\r\\n.");
        if ((char)buffer[end + 2 + length] != '\r' || (char)buffer[end + 2 + length + 1] != '\n')
            throw new InvalidOperationException($"Bulk string does not end with \\r\\n.");
        var value = Encoding.UTF8.GetString(buffer, end + 2, length);
        return (new BulkStringMessage(value), end + 2 + length + 2);
    }
}