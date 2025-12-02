using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Microsoft.Extensions.Hosting;

namespace Cyviz.SignalR;

public class EdgeSimulator
{
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly IEnumerable<string> _deviceIds;

    public EdgeSimulator(IConfiguration config, IHostApplicationLifetime lifetime)
    {
        // Use port 3000 by default to match launchSettings.json
        _baseUrl = config.GetValue<string>("BaseUrl") ?? "http://localhost:3000";
        _apiKey = config.GetValue<string>("ApiKey") ?? "local-dev-key";
        _deviceIds = Enumerable.Range(1, 20).Select(i => $"device-{i:00}");
        
        // Wait for application to start before connecting simulators
        lifetime.ApplicationStarted.Register(() => _ = Run());
    }

    private async Task Run()
    {
        foreach (var id in _deviceIds)
        {
            _ = Task.Run(() => RunDevice(id));
        }
    }

    private async Task RunDevice(string deviceId)
    {
        // Add delay to ensure server is fully started
        await Task.Delay(TimeSpan.FromSeconds(2));
        
        var url = new Uri(new Uri(_baseUrl), "/devicehub");
        Log.Information("EdgeSimulator {DeviceId} connecting to {Url}", deviceId, url);
        
        var conn = new HubConnectionBuilder()
            .WithUrl(url, opts => { opts.Headers.Add("X-Api-Key", _apiKey); })
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        conn.On<string, string, string>("command", async (id, cmd, commandId) =>
        {
            Log.Information("Sim {DeviceId} received command {Cmd} (ID: {CommandId})", deviceId, cmd, commandId);
            
            // Simulate command execution
            await Task.Delay(Random.Shared.Next(100, 500));
            
            // Simulate success/failure (95% success rate)
            var success = Random.Shared.NextDouble() > 0.05;
            var result = success ? $"{cmd} executed successfully" : $"Device failed to execute {cmd}";
            
            // Report command result back to server
            try
            {
                await conn.InvokeAsync("CommandResult", commandId, success ? "Completed" : "Failed", result);
                Log.Information("Sim {DeviceId} reported command {CommandId} result: {Status}", deviceId, commandId, success ? "Completed" : "Failed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Sim {DeviceId} failed to report command result", deviceId);
            }
        });

        while (true)
        {
            try
            {
                await conn.StartAsync();
                await conn.InvokeAsync("Register", deviceId);
                Log.Information("Sim {DeviceId} connected", deviceId);

                while (conn.State == HubConnectionState.Connected)
                {
                    var tele = new 
                    { 
                        timestamp = DateTime.UtcNow,
                        temperature = 20 + Random.Shared.NextDouble() * 20,  // 20-40°C
                        cpuUsage = Random.Shared.NextDouble() * 100,         // 0-100%
                        memoryUsage = 30 + Random.Shared.NextDouble() * 40   // 30-70%
                    };
                    await conn.InvokeAsync("Telemetry", deviceId, System.Text.Json.JsonSerializer.Serialize(tele));
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Sim {DeviceId} reconnecting", deviceId);
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
