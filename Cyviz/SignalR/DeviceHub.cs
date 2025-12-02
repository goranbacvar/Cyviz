using Microsoft.AspNetCore.SignalR;
using Serilog;
using Cyviz.Infrastructure;
using Cyviz.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cyviz.SignalR;

public class DeviceHub : Hub
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ControlHub> _controlHub;
    
    public DeviceHub(AppDbContext db, IHubContext<ControlHub> controlHub) 
    { 
        _db = db; 
        _controlHub = controlHub;
    }
    
    public async Task Register(string deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");
        Log.Information("Device {DeviceId} registered {Conn}", deviceId, Context.ConnectionId);
    }

    public async Task Telemetry(string deviceId, string json)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device != null)
        {
            device.LastSeenUtc = DateTime.UtcNow;
            
            var telemetry = new DeviceTelemetry 
            { 
                Id = Guid.NewGuid(), 
                DeviceId = deviceId, 
                TimestampUtc = DateTime.UtcNow, 
                Json = json 
            };
            
            await _db.Telemetry.AddAsync(telemetry);
            
            var count = await _db.Telemetry.CountAsync(t => t.DeviceId == deviceId);
            if (count > 50)
            {
                var toDelete = await _db.Telemetry.Where(t => t.DeviceId == deviceId).OrderBy(t => t.TimestampUtc).Take(count - 50).ToListAsync();
                _db.Telemetry.RemoveRange(toDelete);
            }
            await _db.SaveChangesAsync();
            
            // Broadcast to ControlHub clients (dashboard)
            await _controlHub.Clients.All.SendAsync("DeviceTelemetryReceived", telemetry);
        }
    }
    
    public async Task CommandResult(string commandId, string status, string result)
    {
        Log.Information("Received command result for {CommandId}: {Status}", commandId, status);
        
        if (!Guid.TryParse(commandId, out var cmdGuid))
        {
            Log.Warning("Invalid command ID format: {CommandId}", commandId);
            return;
        }
        
        var command = await _db.Commands.FirstOrDefaultAsync(c => c.Id == cmdGuid);
        if (command != null)
        {
            command.Status = status;
            command.Result = result;
            command.LatencyMs = command.LatencyMs ?? (long)(DateTime.UtcNow - command.CreatedUtc).TotalMilliseconds;
            
            await _db.SaveChangesAsync();
            
            Log.Information("Updated command {CommandId} status to {Status}", commandId, status);
            
            // Broadcast to ControlHub clients (dashboard)
            await _controlHub.Clients.All.SendAsync("CommandCompleted", command);
        }
        else
        {
            Log.Warning("Command not found: {CommandId}", commandId);
        }
    }
}
