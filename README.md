# Cyviz - Device Control System

A real-time device control system built with Blazor Server, SignalR, and Entity Framework Core.

## Overview

Cyviz is an IoT control platform for managing display walls, codecs, and cameras in control rooms. It provides:

- **Real-time Control**: Send commands to devices via SignalR
- **Live Telemetry**: Monitor device status and metrics in real-time
- **SQLite Database**: Persistent storage with EF Core migrations
- **Command Pipeline**: Resilient command routing with retry logic
- **Chaos Testing**: Built-in chaos engineering for resilience testing

## Quick Start

### Prerequisites
- .NET 6.0 SDK or later
- Visual Studio 2022 or VS Code

### Build and Run

```bash
# Clone repository
git clone https://github.com/yourusername/cyviz.git
cd cyviz

# Restore dependencies
dotnet restore

# Run application
dotnet run --project Cyviz
```

Application will start at:
- **Dashboard**: `http://localhost:3000/dashboard` **NEW!**
- **Main App**: `https://localhost:3001`

### New Dashboard

Access the comprehensive device control dashboard at **http://localhost:3000/dashboard**

**Features:**
- Devices table with filters, sorting, and pagination
- Device detail drawer with live telemetry charts
- Command execution with visible idempotency keys
- Real-time toast notifications
- Live telemetry visualization (last minute)

See [DASHBOARD-QUICKSTART.md](DASHBOARD-QUICKSTART.md) for details.

### Database Setup

Database is automatically created and migrated on first run:
- SQLite database: `Cyviz/app.db`
- Seeds 20 sample devices on startup
- Migrations in `Cyviz/Migrations/`

## Documentation

**Complete Documentation:**

- **[Architecture Overview](ARCHITECTURE.md)** - System architecture and communication flow
- **[Data Flow Guide](docs/DATA-FLOW.md)** - How Blazor components access the database
- **[Architecture Diagrams](docs/ARCHITECTURE-DIAGRAMS.md)** - Visual diagrams with ASCII art
- **[Quick Reference](docs/QUICK-REFERENCE.md)** - Code patterns and examples

### Key Concepts

#### Blazor Server Architecture
```
Browser (HTML/JS) — SignalR — Server (C# Components) — EF Core — SQLite
```

All Razor component code runs **on the server**. The browser only displays HTML and sends events via SignalR.

#### SignalR Hubs
- **BlazorHub** (`/_blazor`): Automatic component state sync
- **ControlHub** (`/controlhub`): Operator commands and broadcasts
- **DeviceHub** (`/devicehub`): Device telemetry and command execution

#### Database Access Pattern
```csharp
@inject IDbContextFactory<AppDbContext> DbFactory

protected override async Task OnInitializedAsync()
{
    await using var db = await DbFactory.CreateDbContextAsync();
    devices = await db.Devices.ToListAsync();
}
```

## Project Structure

```
Cyviz/
├─ Program.cs              # Application entry point & DI configuration
├─ Infrastructure/
│  ├─ AppDbContext.cs     # EF Core database context
│  ├─ AppDbContextFactory.cs
│  ├─ SeedData.cs         # Database seeding
│  └─ Migrations/         # EF Core migrations
├─ Domain/
│  ├─ Models.cs           # Device, DeviceCommand, Telemetry entities
│  └─ Protocols.cs        # Protocol adapters
├─ Application/
│  ├─ CommandRouter.cs    # Background service for command routing
│  ├─ DeviceStatusMonitor.cs
│  └─ ValidationExtensions.cs
├─ SignalR/
│  ├─ ControlHub.cs       # Operator communication hub
│  ├─ DeviceHub.cs        # Device communication hub
│  └─ EdgeSimulator.cs    # Device simulation
├─ Api/
│  ├─ DevicesController.cs # REST API endpoints
│  ├─ ApiKeyMiddleware.cs
│  └─ MetricsEndpoint.cs
└─ Pages/                  # Blazor components (*.razor files)
```

## Features

### 1. Dashboard (NEW!)
**Real-time device control dashboard at http://localhost:3000/dashboard**

- **Devices Table**:
  - Search, filter, and sort devices
  - Keyset pagination (10 per page)
  - Live status updates via SignalR
  - Click any row to view details

- **Device Detail Drawer**:
  - Live telemetry chart (last minute)
  - Three metrics: Temperature, CPU, Memory
  - Command form with idempotency keys
  - Auto-generated keys with "Send Again" option
  - Command results with latency and correlation ID
  - Recent command history

- **Toast Notifications**:
  - Status change alerts
  - Command completion/failure notifications
  - Auto-dismiss or manual close

See: [DASHBOARD-README.md](DASHBOARD-README.md) | [DASHBOARD-QUICKSTART.md](DASHBOARD-QUICKSTART.md)

### 2. Device Management
- CRUD operations on devices
- Support for Display, Codec, and Camera types
- Multiple protocol adapters (Extron, Avocor, HTTP)

### 3. Command Pipeline
```
Operator → REST API → Database → CommandRouter → DeviceHub → Device
                          ↓
                   SignalR Broadcast → All Operators
```

### 4. Real-Time Updates
- Live device status monitoring
- Telemetry streaming
- Command completion notifications
- SignalR-powered dashboard updates

### 5. Resilience Features
- Exponential backoff retry
- Circuit breaker pattern
- Idempotency keys
- Chaos testing mode

## Chaos Testing

Enable chaos engineering to test system resilience:

```bash
# Add random latency (1-2 seconds)
set CHAOS_LATENCY=1.0-2.0s

# Drop 10% of messages
set CHAOS_DROP_RATE=0.1

dotnet run --project Cyviz
```

Expected behavior:
- System remains operational
- Automatic retries with jitter
- Circuit breaker prevents cascading failures
- Degraded but graceful service

## API Endpoints

### REST API

```
GET    /api/devices              # List all devices
GET    /api/devices/{id}         # Get device by ID
POST   /api/devices              # Create device
PUT    /api/devices/{id}         # Update device
DELETE /api/devices/{id}         # Delete device
POST   /api/devices/{id}/commands # Execute command

GET    /health                    # Health check
GET    /metrics                   # System metrics
```

### SignalR Hubs

**ControlHub** (for operators):
```csharp
// Server → Client
"DeviceStatusChanged"     // Device status update
"CommandCompleted"        // Command execution result
"DeviceTelemetryReceived" // New telemetry data

// Client → Server
"ExecuteCommand"          // Send command to device
"SubscribeToDevice"       // Subscribe to device updates
```

**DeviceHub** (for devices):
```csharp
// Server → Client
"ExecuteCommand"          // Command to execute

// Client → Server
"ReportTelemetry"         // Send telemetry data
"ReportCommandResult"     // Report command execution result
"Heartbeat"              // Keep-alive ping
```

## Security

- **API Key Authentication**: Required for `/api/*` and `/devicehub` endpoints
- **Server-Side Execution**: All business logic runs on server (Blazor Server model)
- **No Client Database Access**: Browser cannot execute SQL or access connection strings
- **Validation**: FluentValidation on all inputs

## Configuration

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## Database Schema

```sql
Devices (Id, Name, Type, Protocol, Status, LastSeenUtc, Capabilities, ...)
  → One-to-Many
Commands (Id, DeviceId, Command, Status, Result, IdempotencyKey, ...)
  → One-to-Many
Telemetry (Id, DeviceId, TimestampUtc, Json)
```

Migrations managed by Entity Framework Core:
```bash
# Create new migration
dotnet ef migrations add MigrationName --project Cyviz

# Apply migrations
dotnet ef database update --project Cyviz
```

## Development

### Add New Blazor Component

```razor
@page "/my-component"
@inject IDbContextFactory<AppDbContext> DbFactory

<h1>My Component</h1>

@code {
    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        // Query database
    }
}
```

### Add New SignalR Hub Method

```csharp
// ControlHub.cs
public async Task CustomMethod(string data)
{
    // Process data
    
    // Broadcast to all clients
    await Clients.All.SendAsync("CustomEvent", result);
}
```

### Add New Entity

```csharp
// Domain/Models.cs
public class MyEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

// Infrastructure/AppDbContext.cs
public DbSet<MyEntity> MyEntities => Set<MyEntity>();

// Create migration
dotnet ef migrations add AddMyEntity --project Cyviz
```

## Troubleshooting

### "SQLite Error 1: 'no such table: Devices'"
Solution: Delete `app.db` and restart. Migrations will auto-apply.

```bash
rm Cyviz/app.db
dotnet run --project Cyviz
```

### SignalR Connection Fails
- Check HTTPS certificate is trusted
- Verify firewall allows port 5001
- Check browser console for errors

### Database Locked Error
- Only one process can write to SQLite at a time
- Use `IDbContextFactory` pattern (already implemented)
- Ensure `await using` disposes DbContext properly

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit changes: `git commit -am 'Add my feature'`
4. Push to branch: `git push origin feature/my-feature`
5. Create Pull Request

## License

MIT License - see LICENSE file for details

## Resources

- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor/)
- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr/)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [SQLite Documentation](https://www.sqlite.org/docs.html)

## Architecture Highlights

### Why Blazor Server?
- **Zero JavaScript**: Write full-stack C# applications
- **Small Payload**: ~30KB initial download vs 2MB+ for WASM
- **Server-Side Security**: All logic runs on server
- **Real-Time by Default**: SignalR built-in

### Why SignalR?
- **Bidirectional**: Server can push updates to clients
- **Persistent Connection**: No polling needed
- **Automatic Reconnection**: Handles network issues gracefully
- **Scalable**: Supports thousands of concurrent connections

### Why SQLite?
- **Zero Configuration**: No server installation required
- **Single File**: Easy deployment and backup
- **Fast**: Sufficient for small-to-medium scale
- **Portable**: Works on Windows, Linux, macOS

---

**Built with love using Blazor Server, SignalR, and Entity Framework Core**
