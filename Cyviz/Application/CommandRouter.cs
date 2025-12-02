using System.Threading.Channels;
using Cyviz.Infrastructure;
using Cyviz.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.AspNetCore.SignalR;
using Cyviz.SignalR;
using Microsoft.Extensions.Hosting;

namespace Cyviz.Application;

public enum CommandEnqueueResultStatus { Accepted, QueueFull }
public record CommandEnqueueResult(CommandEnqueueResultStatus Status, Guid CommandId)
{
    public static CommandEnqueueResult QueueFull => new(CommandEnqueueResultStatus.QueueFull, Guid.Empty);
}

public class CommandRouter : BackgroundService
{
    private readonly Channel<DeviceCommand> _channel = Channel.CreateBounded<DeviceCommand>(50);
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IHubContext<DeviceHub> _deviceHub;
    private readonly DeviceCircuits _circuits = new();

    public CommandRouter(IDbContextFactory<AppDbContext> dbFactory, IHubContext<DeviceHub> deviceHub)
    {
        _dbFactory = dbFactory; _deviceHub = deviceHub;
    }

    public async Task<CommandEnqueueResult> EnqueueAsync(string deviceId, string idempotencyKey, string command, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Commands.FirstOrDefaultAsync(c => c.DeviceId == deviceId && c.IdempotencyKey == idempotencyKey, ct);
        if (existing != null) return new(CommandEnqueueResultStatus.Accepted, existing.Id);
        var cmd = new DeviceCommand { Id = Guid.NewGuid(), DeviceId = deviceId, IdempotencyKey = idempotencyKey, Command = command, CreatedUtc = DateTime.UtcNow, Status = "Pending" };
        if (!_channel.Writer.TryWrite(cmd)) return CommandEnqueueResult.QueueFull;
        await db.Commands.AddAsync(cmd, ct);
        await db.SaveChangesAsync(ct);
        return new(CommandEnqueueResultStatus.Accepted, cmd.Id);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var cmd in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var cb = _circuits.Get(cmd.DeviceId);
                if (cb.IsOpen) { Log.Warning("Circuit open for {DeviceId}", cmd.DeviceId); continue; }

                var dropped = ChaosSettings.DropRate > 0 && new Random().NextDouble() < ChaosSettings.DropRate;
                if (dropped)
                {
                    Log.Warning("Chaos drop command {CommandId}", cmd.Id);
                    continue;
                }

                if (ChaosSettings.LatencyMaxMs > 0)
                {
                    var delay = new Random().Next((int)ChaosSettings.LatencyMinMs, (int)ChaosSettings.LatencyMaxMs);
                    await Task.Delay(delay, stoppingToken);
                }

                var start = DateTime.UtcNow;
                var ok = await RetryPolicy.ExecuteAsync(async () =>
                {
                    // Send command with command ID so device can report back result
                    await _deviceHub.Clients.Group($"device:{cmd.DeviceId}").SendAsync("command", cmd.DeviceId, cmd.Command, cmd.Id.ToString(), stoppingToken);
                    Log.Information("Command {CommandId} sent to device group 'device:{DeviceId}'", cmd.Id, cmd.DeviceId);
                    return true; // Command sent successfully
                }, stoppingToken);

                if (!ok)
                {
                    cb.RecordFailure();
                    cmd.Status = "Failed";
                    cmd.Result = "Failed to send command to device";
                    cmd.LatencyMs = (long)(DateTime.UtcNow - start).TotalMilliseconds;
                    
                    await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);
                    db.Update(cmd);
                    await db.SaveChangesAsync(stoppingToken);
                    
                    // Broadcast failure
                    await _deviceHub.Clients.All.SendAsync("CommandCompleted", cmd, stoppingToken);
                }
                else
                {
                    cb.RecordSuccess();
                    
                    // Set timeout for command response (10 seconds)
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        
                        // Check if command is still pending
                        await using var timeoutDb = await _dbFactory.CreateDbContextAsync();
                        var timeoutCmd = await timeoutDb.Commands.FirstOrDefaultAsync(c => c.Id == cmd.Id);
                        
                        if (timeoutCmd != null && timeoutCmd.Status == "Pending")
                        {
                            timeoutCmd.Status = "Failed";
                            timeoutCmd.Result = "Command timeout - device did not respond within 10 seconds";
                            timeoutCmd.LatencyMs = (long)(DateTime.UtcNow - timeoutCmd.CreatedUtc).TotalMilliseconds;
                            await timeoutDb.SaveChangesAsync();
                            
                            Log.Warning("Command {CommandId} timed out for device {DeviceId}", cmd.Id, cmd.DeviceId);
                            
                            // Broadcast timeout
                            await _deviceHub.Clients.All.SendAsync("CommandCompleted", timeoutCmd);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed routing command {CommandId}");
            }
        }
    }
}
