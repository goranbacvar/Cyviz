namespace Cyviz.Domain;

public enum DeviceType { Display, Codec, Switcher, Sensor }
public enum DeviceProtocol { TcpLine, HttpJson, EdgeSignalR }
public enum DeviceStatus { Online, Offline }

public class Device
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DeviceType Type { get; set; }
    public DeviceProtocol Protocol { get; set; }
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public DeviceStatus Status { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string Firmware { get; set; } = "v1.0.0";
    public string Location { get; set; } = "";
    public byte[]? RowVersion { get; set; }
}

public class DeviceCommand
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public string Command { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = "Pending"; // Pending|Completed|Failed
    public string? Result { get; set; }
    public long? LatencyMs { get; set; }
}

public class DeviceTelemetry
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = default!;
    public DateTime TimestampUtc { get; set; }
    public string Json { get; set; } = default!;
}
