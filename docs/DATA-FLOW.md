# Data Flow: Browser to Database

## Overview

This document explains how Razor components in the browser communicate with the SQLite database in the Cyviz Blazor Server application.

---

## Architecture Type: Blazor Server

**Important**: This is a **Blazor Server** application, not Blazor WebAssembly. The key difference:

- **Blazor Server**: Razor components run on the server via SignalR connection
- **Blazor WebAssembly**: Razor components run in the browser (requires HTTP API calls)

---

## High-Level Data Flow

```
???????????????????????????????????????????????????????????????????????
?                         BROWSER (Client)                            ?
?                                                                     ?
?  ????????????????????????????????????????????????????????????????  ?
?  ?  Razor Component (.razor file)                               ?  ?
?  ?  - Renders HTML                                              ?  ?
?  ?  - User interactions (clicks, input)                         ?  ?
?  ????????????????????????????????????????????????????????????????  ?
?                     ?                                              ?
?                     ? SignalR Connection (WebSocket)               ?
?                     ? (Sends: user events, state changes)          ?
?                     ? (Receives: HTML diffs, state updates)        ?
??????????????????????????????????????????????????????????????????????
                      ?
                      ? HTTPS / WSS
                      ?
??????????????????????????????????????????????????????????????????????
?                      ASP.NET CORE SERVER                           ?
?                                                                     ?
?  ????????????????????????????????????????????????????????????????  ?
?  ?  Blazor SignalR Hub (BlazorHub)                              ?  ?
?  ?  - Manages component lifecycle                               ?  ?
?  ?  - Executes Razor component code                             ?  ?
?  ?  - Sends HTML diffs back to browser                          ?  ?
?  ????????????????????????????????????????????????????????????????  ?
?                     ?                                              ?
?                     ? Dependency Injection                         ?
?                     ?                                              ?
?  ????????????????????????????????????????????????????????????????  ?
?  ?  Services / DbContext                                        ?  ?
?  ?  - IDbContextFactory<AppDbContext>                           ?  ?
?  ?  - Business logic services                                   ?  ?
?  ????????????????????????????????????????????????????????????????  ?
?                     ?                                              ?
?                     ? Entity Framework Core                        ?
?                     ?                                              ?
?  ????????????????????????????????????????????????????????????????  ?
?  ?  SQLite Database (app.db)                                    ?  ?
?  ?  - Devices table                                             ?  ?
?  ?  - Commands table                                            ?  ?
?  ?  - Telemetry table                                           ?  ?
?  ????????????????????????????????????????????????????????????????  ?
?                                                                     ?
???????????????????????????????????????????????????????????????????????
```

---

## Detailed Flow: User Views Device List

### Step 1: Browser Loads Page

```
Browser: GET /devices
  ?
Server: Serves _Host.cshtml with Blazor script
  ?
Browser: Establishes SignalR connection to /_blazor
  ?
Server: Creates circuit and component instance
```

### Step 2: Component Initialization (Server-Side)

**Hypothetical Devices.razor Component:**
```razor
@page "/devices"
@inject IDbContextFactory<AppDbContext> DbFactory

<h1>Devices</h1>

@if (devices == null)
{
    <p>Loading...</p>
}
else
{
    <table>
        @foreach (var device in devices)
        {
            <tr>
                <td>@device.Name</td>
                <td>@device.Status</td>
            </tr>
        }
    </table>
}

@code {
    private List<Device>? devices;

    protected override async Task OnInitializedAsync()
    {
        // THIS CODE RUNS ON THE SERVER
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
    }
}
```

**What Happens:**

```
???????????????????????????????????????????????????????????????????????
? 1. OnInitializedAsync() executes ON THE SERVER                     ?
?    ?                                                                ?
? 2. IDbContextFactory injected via DI                                ?
?    ?                                                                ?
? 3. DbContext created: await DbFactory.CreateDbContextAsync()        ?
?    ?                                                                ?
? 4. EF Core executes SQL: SELECT * FROM Devices                      ?
?    ?                                                                ?
? 5. SQLite returns rows                                              ?
?    ?                                                                ?
? 6. EF Core maps rows to List<Device>                                ?
?    ?                                                                ?
? 7. Component re-renders with data                                   ?
?    ?                                                                ?
? 8. Blazor computes HTML diff                                        ?
?    ?                                                                ?
? 9. SignalR sends diff to browser                                    ?
?    ?                                                                ?
? 10. Browser applies diff and displays table                         ?
???????????????????????????????????????????????????????????????????????
```

---

## Key Concepts

### 1. **Server-Side Execution**

```csharp
@code {
    protected override async Task OnInitializedAsync()
    {
        // ?? THIS CODE RUNS ON THE SERVER, NOT IN THE BROWSER
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
        // The browser NEVER sees this code
        // The browser ONLY receives the rendered HTML
    }
}
```

### 2. **SignalR Communication**

```
Browser                          Server
   ?                               ?
   ????????? User clicks ?????????>?
   ?        (button event)         ?
   ?                               ?
   ?                               ???> Execute @onclick handler
   ?                               ???> Query database
   ?                               ???> Update component state
   ?                               ???> Re-render component
   ?                               ???> Compute HTML diff
   ?                               ?
   ?<????? HTML diff update ????????
   ?    (only changed elements)    ?
   ?                               ?
   ???> Apply diff to DOM          ?
```

### 3. **Dependency Injection**

```csharp
// Program.cs - Service Registration
builder.Services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// Devices.razor - Service Injection
@inject IDbContextFactory<AppDbContext> DbFactory

@code {
    // Factory is automatically provided by DI container
    // The same factory is shared across all components
}
```

---

## Complete Example: CRUD Operations

### Create (Add Device)

```razor
@page "/devices/create"
@inject IDbContextFactory<AppDbContext> DbFactory
@inject NavigationManager Navigation

<h2>Add Device</h2>

<input @bind="deviceName" placeholder="Device Name" />
<button @onclick="SaveDevice">Save</button>

@code {
    private string deviceName = "";

    private async Task SaveDevice()
    {
        // Executes on server
        await using var db = await DbFactory.CreateDbContextAsync();
        
        var device = new Device 
        { 
            Id = Guid.NewGuid().ToString(),
            Name = deviceName,
            Status = DeviceStatus.Offline
        };
        
        db.Devices.Add(device);
        await db.SaveChangesAsync(); // Writes to SQLite
        
        Navigation.NavigateTo("/devices"); // Redirect
    }
}
```

**Flow:**
```
1. User types in input (SignalR sends value to server)
2. User clicks Save button (SignalR sends click event)
3. Server executes SaveDevice() method
4. Server creates DbContext from factory
5. Server adds Device to DbSet
6. Server calls SaveChangesAsync()
7. EF Core generates SQL: INSERT INTO Devices (...)
8. SQLite writes to app.db file
9. Server navigates to /devices
10. SignalR tells browser to navigate
```

### Read (List Devices)

```razor
@page "/devices"
@inject IDbContextFactory<AppDbContext> DbFactory
@implements IDisposable

<h1>Devices</h1>

<table>
    <thead>
        <tr>
            <th>Name</th>
            <th>Status</th>
            <th>Last Seen</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var device in devices)
        {
            <tr>
                <td>@device.Name</td>
                <td>@device.Status</td>
                <td>@device.LastSeenUtc?.ToString("g")</td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<Device> devices = new();
    private AppDbContext? db;

    protected override async Task OnInitializedAsync()
    {
        // Create DbContext that lives for component lifetime
        db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public void Dispose()
    {
        db?.Dispose(); // Clean up DbContext
    }
}
```

**Flow:**
```
1. Browser requests /devices
2. Server creates Devices component instance
3. Server calls OnInitializedAsync()
4. Server creates DbContext from factory
5. Server queries: SELECT * FROM Devices ORDER BY Name
6. SQLite returns rows
7. EF Core materializes List<Device>
8. Server renders component with data
9. Server sends HTML to browser via SignalR
10. Browser displays table
```

### Update (Edit Device)

```razor
@page "/devices/edit/{deviceId}"
@inject IDbContextFactory<AppDbContext> DbFactory

<h2>Edit Device</h2>

@if (device == null)
{
    <p>Loading...</p>
}
else
{
    <input @bind="device.Name" />
    <select @bind="device.Status">
        <option value="@DeviceStatus.Online">Online</option>
        <option value="@DeviceStatus.Offline">Offline</option>
    </select>
    <button @onclick="SaveChanges">Save</button>
}

@code {
    [Parameter]
    public string DeviceId { get; set; } = "";
    
    private Device? device;

    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        device = await db.Devices.FindAsync(DeviceId);
    }

    private async Task SaveChanges()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        db.Devices.Update(device!);
        await db.SaveChangesAsync();
    }
}
```

**Flow:**
```
1. User navigates to /devices/edit/device-01
2. Server extracts DeviceId parameter
3. Server queries: SELECT * FROM Devices WHERE Id = 'device-01'
4. User modifies input fields (SignalR syncs values)
5. User clicks Save
6. Server attaches entity to new DbContext
7. Server calls SaveChangesAsync()
8. EF Core generates: UPDATE Devices SET Name = ... WHERE Id = ...
9. SQLite updates row
```

### Delete (Remove Device)

```razor
<button @onclick="() => DeleteDevice(device.Id)">Delete</button>

@code {
    private async Task DeleteDevice(string deviceId)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var device = await db.Devices.FindAsync(deviceId);
        if (device != null)
        {
            db.Devices.Remove(device);
            await db.SaveChangesAsync();
        }
        
        // Refresh list
        devices = await db.Devices.ToListAsync();
    }
}
```

---

## Real-Time Updates with SignalR Hubs

The application also has custom SignalR hubs for real-time device communication:

```
????????????????                    ????????????????
?   Browser    ?                    ?   Device     ?
?  (Operator)  ?                    ?   (Edge)     ?
????????????????                    ????????????????
       ?                                    ?
       ? SignalR                   SignalR ?
       ?                                    ?
????????????????????????????????????????????????????
?           ASP.NET Core Server                    ?
?                                                   ?
?  ??????????????????      ???????????????????    ?
?  ?  ControlHub    ?      ?   DeviceHub     ?    ?
?  ?  (Operators)   ????????   (Devices)     ?    ?
?  ??????????????????      ???????????????????    ?
?           ?                       ?              ?
?           ?    ???????????????????????????       ?
?           ?    ?  CommandRouter          ?       ?
?           ?    ?  (Background Service)   ?       ?
?           ?    ???????????????????????????       ?
?           ?               ?                      ?
?  ?????????????????????????????????????          ?
?  ?     AppDbContext (EF Core)        ?          ?
?  ?????????????????????????????????????          ?
?                  ?                               ?
?  ?????????????????????????????????????          ?
?  ?     SQLite Database (app.db)      ?          ?
?  ?????????????????????????????????????          ?
?                                                   ?
?????????????????????????????????????????????????????
```

**Example: Real-Time Device Status Update**

```csharp
// DeviceHub.cs (receives from devices)
public async Task ReportTelemetry(DeviceTelemetry telemetry)
{
    // Save to database
    await using var db = await _dbFactory.CreateDbContextAsync();
    db.Telemetry.Add(telemetry);
    await db.SaveChangesAsync();
    
    // Broadcast to all operators
    await _controlHub.Clients.All.SendAsync("DeviceTelemetryReceived", telemetry);
}
```

```razor
@* Devices.razor (in browser) *@
@inject IHubContext<ControlHub> HubContext
@implements IAsyncDisposable

<h1>Live Devices</h1>

@foreach (var device in devices)
{
    <div class="device-card @GetStatusClass(device)">
        @device.Name - @device.Status
    </div>
}

@code {
    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to real-time updates
        hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/controlhub"))
            .Build();

        hubConnection.On<DeviceTelemetry>("DeviceTelemetryReceived", async (telemetry) =>
        {
            // Update UI when telemetry arrives
            var device = devices.FirstOrDefault(d => d.Id == telemetry.DeviceId);
            if (device != null)
            {
                device.LastSeenUtc = telemetry.TimestampUtc;
                await InvokeAsync(StateHasChanged); // Re-render
            }
        });

        await hubConnection.StartAsync();
        
        // Load initial data from database
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

---

## Performance Considerations

### 1. **DbContext Lifetime**

```csharp
// ? BAD: Singleton DbContext (causes concurrency issues)
builder.Services.AddDbContext<AppDbContext>(ServiceLifetime.Singleton);

// ? GOOD: Factory pattern for Blazor Server
builder.Services.AddDbContextFactory<AppDbContext>();

// Usage in components:
await using var db = await DbFactory.CreateDbContextAsync();
```

### 2. **Async All The Way**

```csharp
// ? BAD: Blocking call in Blazor
var devices = db.Devices.ToList(); // Blocks SignalR thread

// ? GOOD: Async
var devices = await db.Devices.ToListAsync(); // Non-blocking
```

### 3. **No Tracking for Read-Only**

```csharp
// ? Better performance for read-only queries
var devices = await db.Devices
    .AsNoTracking() // Don't track changes
    .ToListAsync();
```

### 4. **Dispose DbContext Properly**

```csharp
// Pattern 1: Using statement (preferred)
await using var db = await DbFactory.CreateDbContextAsync();
var devices = await db.Devices.ToListAsync();
// DbContext auto-disposed here

// Pattern 2: IDisposable component
@implements IAsyncDisposable

@code {
    private AppDbContext? db;

    protected override async Task OnInitializedAsync()
    {
        db = await DbFactory.CreateDbContextAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }
    }
}
```

---

## Security Model

### Authentication Flow

```
???????????????????????????????????????????????????????????????????????
?                         Browser (Client)                            ?
?                                                                     ?
?  - No direct database access                                        ?
?  - Cannot execute SQL                                               ?
?  - Cannot see connection strings                                    ?
?  - Only receives rendered HTML + JS                                 ?
?                                                                     ?
???????????????????????????????????????????????????????????????????????
                               ?
                               ? SignalR over HTTPS
                               ?
???????????????????????????????????????????????????????????????????????
?                      Server (Trusted Boundary)                      ?
?                                                                     ?
?  ? All database operations happen here                             ?
?  ? Connection string is server-side only                           ?
?  ? Server-side validation                                          ?
?  ? Authorization checks                                            ?
?                                                                     ?
???????????????????????????????????????????????????????????????????????
```

### API Key Middleware (for external API/devices)

```csharp
// Program.cs
builder.Services.AddSingleton<ApiKeyValidator>();
app.UseMiddleware<ApiKeyMiddleware>();

// ApiKeyMiddleware.cs
public class ApiKeyMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey))
            {
                context.Response.StatusCode = 401;
                return;
            }
            
            if (!_validator.IsValid(apiKey))
            {
                context.Response.StatusCode = 403;
                return;
            }
        }
        
        await _next(context);
    }
}
```

---

## Debugging Data Flow

### Enable EF Core Logging

```csharp
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Output:**
```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (2ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT "d"."Id", "d"."Name", "d"."Status", "d"."Type"
      FROM "Devices" AS "d"
      ORDER BY "d"."Name"
```

### Blazor Server Reconnection

```razor
<script src="_framework/blazor.server.js"></script>
<script>
    Blazor.start({
        reconnectionOptions: {
            maxRetries: 10,
            retryIntervalMilliseconds: 3000
        }
    });
</script>
```

---

## Summary

### Key Takeaways

1. **Blazor Server = Server-Side Rendering**: All C# code in `@code` blocks runs on the server, not in the browser

2. **SignalR = Bridge**: SignalR maintains a persistent connection and syncs UI state between browser and server

3. **DbContext Factory**: Use `IDbContextFactory<AppDbContext>` in Blazor Server for proper lifetime management

4. **Security by Default**: Browser never has direct database access; all queries execute server-side

5. **Real-Time Updates**: Combine Blazor components with SignalR hubs for live data synchronization

### Data Flow Summary

```
Browser UI ?(SignalR)?> Server Razor Component ?(DI)?> DbContext ?(EF Core)?> SQLite
                                                                              
Browser UI <?(SignalR)? Server Razor Component <?(LINQ)? DbContext <?(SQL)?? SQLite
```

**The browser is just a "dumb terminal" displaying HTML. All logic, database access, and state management happens on the server.**

