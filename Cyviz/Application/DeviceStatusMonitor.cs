using Cyviz.Infrastructure;
using Cyviz.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using Cyviz.SignalR;
using Serilog;

namespace Cyviz.Application;

public class DeviceStatusMonitor : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<ControlHub> _controlHub;
    private readonly TimeSpan offlineThreshold = TimeSpan.FromSeconds(30);

    public DeviceStatusMonitor(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache, IHubContext<ControlHub> controlHub)
    {
        _dbFactory = dbFactory; 
        _cache = cache;
        _controlHub = controlHub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);
            var now = DateTime.UtcNow;
            var devices = await db.Devices.ToListAsync(stoppingToken);
            
            foreach (var d in devices)
            {
                var oldStatus = d.Status;
                
                if (d.LastSeenUtc.HasValue && (now - d.LastSeenUtc.Value) > offlineThreshold && d.Status != DeviceStatus.Offline)
                {
                    d.Status = DeviceStatus.Offline;
                    
                    // Broadcast status change
                    await _controlHub.Clients.All.SendAsync("DeviceStatusChanged", d.Id, d.Status, stoppingToken);
                    Log.Information("Device {DeviceId} status changed: {OldStatus} -> {NewStatus}", d.Id, oldStatus, d.Status);
                }
                else if (d.LastSeenUtc.HasValue && (now - d.LastSeenUtc.Value) <= offlineThreshold && d.Status != DeviceStatus.Online)
                {
                    d.Status = DeviceStatus.Online;
                    
                    // Broadcast status change
                    await _controlHub.Clients.All.SendAsync("DeviceStatusChanged", d.Id, d.Status, stoppingToken);
                    Log.Information("Device {DeviceId} status changed: {OldStatus} -> {NewStatus}", d.Id, oldStatus, d.Status);
                }
            }
            
            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
