using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Cyviz.SignalR;

public class ControlHub : Hub
{
    public override Task OnConnectedAsync()
    {
        Log.Information("Control client connected {Conn}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public async Task Reconnecting()
    {
        await Clients.All.SendAsync("Notify", "Connection Lost", "Attempting to reconnect...", "warning");
    }

    public async Task Reconnected()
    {
        await Clients.All.SendAsync("Notify", "Reconnected", "Connection restored successfully", "success");
    }
}
