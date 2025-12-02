using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cyviz.Infrastructure;
using Cyviz.Domain;
using Cyviz.Application;

namespace Cyviz.IntegrationTests;

public class CyvizWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext registrations
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(IDbContextFactory<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                .ToList();
            
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
            
            // Add in-memory database for testing
            var dbName = "TestDb_" + Guid.NewGuid();
            
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
            
            // Register scoped AppDbContext for controllers (from factory)
            services.AddScoped<AppDbContext>(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
                return factory.CreateDbContext();
            });

            // Remove background services that might interfere with tests
            services.RemoveAll(typeof(IHostedService));
            
            // Ensure services needed by tests are available
            if (!services.Any(x => x.ServiceType == typeof(CommandRouter)))
            {
                services.AddSingleton<CommandRouter>();
            }
        });

        builder.UseEnvironment("Test");

        // Suppress logging during tests to keep output clean
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        
        // Seed test data
        using var scope = host.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        
        db.Database.EnsureCreated();
        SeedTestData(db);
        
        return host;
    }

    private void SeedTestData(AppDbContext db)
    {
        if (db.Devices.Any()) return;

        var testDevices = new[]
        {
            new Device
            {
                Id = "device-01",
                Name = "TestDisplay-1",
                Type = DeviceType.Display,
                Protocol = DeviceProtocol.HttpJson,
                Capabilities = new[] { "Ping", "GetStatus", "Reboot" },
                Status = DeviceStatus.Online,
                LastSeenUtc = DateTime.UtcNow,
                Firmware = "v1.0.0",
                Location = "TestRoom"
            },
            new Device
            {
                Id = "device-02",
                Name = "TestCodec-1",
                Type = DeviceType.Codec,
                Protocol = DeviceProtocol.TcpLine,
                Capabilities = new[] { "Ping", "GetStatus" },
                Status = DeviceStatus.Offline,
                LastSeenUtc = DateTime.UtcNow.AddHours(-2),
                Firmware = "v2.1.0",
                Location = "TestRoom"
            },
            new Device
            {
                Id = "device-03",
                Name = "TestSwitcher-1",
                Type = DeviceType.Switcher,
                Protocol = DeviceProtocol.EdgeSignalR,
                Capabilities = new[] { "Ping", "GetStatus", "Reboot", "SwitchInput" },
                Status = DeviceStatus.Online,
                LastSeenUtc = DateTime.UtcNow,
                Firmware = "v1.5.2",
                Location = "ControlRoom"
            }
        };

        db.Devices.AddRange(testDevices);
        db.SaveChanges();
    }
}
