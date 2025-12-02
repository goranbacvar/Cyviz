using System.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Cyviz.Api;
using Cyviz.Infrastructure;
using Cyviz.SignalR;
using Cyviz.Application;
using Cyviz.Domain;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// EF Core SQLite - use absolute path to avoid working directory issues
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "app.db");

// Register DbContextFactory for background services (singleton-safe)
builder.Services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// Register scoped DbContext for API controllers
builder.Services.AddScoped<AppDbContext>(sp =>
{
    var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
    return factory.CreateDbContext();
});

// IMemoryCache
builder.Services.AddMemoryCache();

// SignalR
builder.Services.AddSignalR();

// API key authentication middleware
builder.Services.AddSingleton<ApiKeyValidator>();

// Command pipeline
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CommandRouter>());
builder.Services.AddHostedService<DeviceStatusMonitor>();
builder.Services.AddSingleton<DeviceCircuits>();
builder.Services.AddSingleton<EdgeSimulator>();

// FluentValidation
builder.Services.AddValidators();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Cyviz"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation());

// Controllers/API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

// Validate application license on startup
Cyviz.Application.LicenseValidator.ValidateOnStartup();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Disabled HTTPS redirection for development to avoid certificate issues
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// API Key middleware
app.UseMiddleware<ApiKeyMiddleware>();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// SignalR hubs
app.MapHub<ControlHub>("/controlhub");
app.MapHub<DeviceHub>("/devicehub");

// API endpoints
app.MapControllers();

// Health and metrics
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/metrics", MetricsEndpoint.Handle);

// Diagnostics (development only)
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/diagnostics/devices", DiagnosticsEndpoint.GetConnectedDevices);
    app.MapPost("/api/diagnostics/test-command/{deviceId}", DiagnosticsEndpoint.TestDeviceCommand);
}

// Apply migrations & seed devices
if (!app.Environment.IsEnvironment("Test"))
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeedAsync(db, scope.ServiceProvider.GetRequiredService<IMemoryCache>());
    
    var deviceCount = await db.Devices.CountAsync();
    Log.Information("Seeding complete. Device count: {Count}", deviceCount);
}

// Load chaos settings and start edge simulator
ChaosSettings.Load(app.Configuration);
app.Services.GetRequiredService<EdgeSimulator>();

app.Run();

// Make Program accessible to test projects
public partial class Program { }
