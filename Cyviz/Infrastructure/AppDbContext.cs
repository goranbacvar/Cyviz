using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using Cyviz.Domain;

namespace Cyviz.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceCommand> Commands => Set<DeviceCommand>();
    public DbSet<DeviceTelemetry> Telemetry => Set<DeviceTelemetry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Convert string[] Capabilities to a JSON string for storage (SQLite TEXT)
        var capabilitiesConverter = new ValueConverter<string[], string>(
            v => JsonSerializer.Serialize(v ?? Array.Empty<string>(), (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
        );

        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.RowVersion).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
            e.HasIndex(d => new { d.Status, d.Type });

            // Map Capabilities as JSON in a TEXT column (SQLite)
            e.Property(d => d.Capabilities)
             .HasConversion(capabilitiesConverter)
             .HasColumnType("TEXT");
        });
        modelBuilder.Entity<DeviceCommand>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.DeviceId, c.IdempotencyKey }).IsUnique();
        });
        modelBuilder.Entity<DeviceTelemetry>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.DeviceId, t.TimestampUtc });
        });
    }
}
