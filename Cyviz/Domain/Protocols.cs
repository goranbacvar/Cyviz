namespace Cyviz.Domain;

public interface IDeviceProtocolAdapter
{
    Task ConnectAsync(Device device, CancellationToken token);
    Task<CommandResult> SendCommandAsync(Device device, DeviceCommand command, CancellationToken token);
    IAsyncEnumerable<DeviceTelemetry> StreamTelemetryAsync(Device device, CancellationToken token);
}

public record CommandResult(bool Success, string? Output, string? Error);

// Example adapter registration point; implementations would live under Infrastructure.Protocols
