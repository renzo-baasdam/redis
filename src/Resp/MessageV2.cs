namespace Redis;

public abstract record MessageV2();
public sealed record SimpleStringMessage(string Value) : MessageV2;
public sealed record BulkStringMessage(string Value) : MessageV2;
public sealed record ArrayMessage(List<MessageV2> Values) : MessageV2;
public sealed record SimpleErrorMessage(string Message) : MessageV2;
