using Cyviz.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Cyviz.Infrastructure;

public static class SeedData
{
    public static async Task EnsureSeedAsync(AppDbContext db, IMemoryCache cache)
    {
        // Defensive: if table missing (migration not applied), ensure created then retry
        try
        {
            if (await db.Devices.AsNoTracking().AnyAsync()) return;
        }
        catch (SqliteException)
        {
            await db.Database.EnsureCreatedAsync();
            if (await db.Devices.AsNoTracking().AnyAsync()) return;
        }
        var rnd = new Random(42);
        var types = Enum.GetValues<DeviceType>();
        var protos = Enum.GetValues<DeviceProtocol>();
        for (int i = 1; i <= 20; i++)
        {
            var dev = new Device
            {
                Id = $"device-{i:00}",
                Name = $"DisplayWall-{i}",
                Type = types[rnd.Next(types.Length)],
                Protocol = protos[rnd.Next(protos.Length)],
                Capabilities = new[] { "Ping", "GetStatus", "Reboot" },
                Status = DeviceStatus.Online,
                LastSeenUtc = DateTime.UtcNow,
                Firmware = "v1.2.3",
                Location = "ControlRoomA"
            };
            db.Devices.Add(dev);
        }
        await db.SaveChangesAsync();
    }
}
