using Microsoft.AspNetCore.SignalR;
using Cyviz.SignalR;

namespace Cyviz.Api;

public static class DiagnosticsEndpoint
{
    public static IResult GetConnectedDevices(IHubContext<DeviceHub> deviceHub)
    {
        // Note: SignalR doesn't expose group membership directly
        // This is a placeholder for diagnostic info
        return Results.Ok(new 
        { 
            message = "Check server logs for device registrations",
            tip = "Look for 'Device {DeviceId} registered' log messages"
        });
    }
    
    public static async Task<IResult> TestDeviceCommand(
        string deviceId, 
        IHubContext<DeviceHub> deviceHub)
    {
        try
        {
            var testCommandId = Guid.NewGuid().ToString();
            await deviceHub.Clients.Group($"device:{deviceId}")
                .SendAsync("command", deviceId, "TEST_PING", testCommandId);
            
            return Results.Ok(new 
            { 
                success = true,
                message = $"Test command sent to device group 'device:{deviceId}'",
                commandId = testCommandId,
                tip = "Check device logs to see if command was received"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Failed to send test command"
            );
        }
    }
}
