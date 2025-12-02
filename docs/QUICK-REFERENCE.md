# Quick Reference: Blazor Server Database Access

## Core Concept

**In Blazor Server, Razor components run on the server, not in the browser.**

```
Browser (Dumb Terminal) ??SignalR?? Server (Smart) ?EF Core?? SQLite
         HTML only                   All C# code runs here
```

---

## How to Access Database in Razor Components

### Step 1: Inject DbContextFactory

```razor
@page "/devices"
@inject IDbContextFactory<AppDbContext> DbFactory

<h1>Devices</h1>
```

### Step 2: Query Database in Lifecycle Methods

```razor
@code {
    private List<Device> devices = new();

    protected override async Task OnInitializedAsync()
    {
        // THIS CODE RUNS ON THE SERVER
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
    }
}
```

### Step 3: Display Data

```razor
<table>
    @foreach (var device in devices)
    {
        <tr>
            <td>@device.Name</td>
            <td>@device.Status</td>
        </tr>
    }
</table>
```

---

## Common Patterns

### Pattern 1: Read-Only List

```razor
@page "/devices"
@inject IDbContextFactory<AppDbContext> DbFactory

<h1>Devices (@devices.Count)</h1>

<table class="table">
    <thead>
        <tr><th>Name</th><th>Status</th><th>Type</th></tr>
    </thead>
    <tbody>
        @foreach (var device in devices)
        {
            <tr>
                <td>@device.Name</td>
                <td>
                    <span class="badge @GetStatusBadge(device.Status)">
                        @device.Status
                    </span>
                </td>
                <td>@device.Type</td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<Device> devices = new();

    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices
            .AsNoTracking() // Read-only, better performance
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    private string GetStatusBadge(DeviceStatus status) => status switch
    {
        DeviceStatus.Online => "bg-success",
        DeviceStatus.Offline => "bg-danger",
        _ => "bg-secondary"
    };
}
```

### Pattern 2: Create Form

```razor
@page "/devices/create"
@inject IDbContextFactory<AppDbContext> DbFactory
@inject NavigationManager Navigation

<h2>Create New Device</h2>

<EditForm Model="newDevice" OnValidSubmit="SaveDevice">
    <DataAnnotationsValidator />
    
    <div class="form-group">
        <label>Device Name:</label>
        <InputText @bind-Value="newDevice.Name" class="form-control" />
        <ValidationMessage For="@(() => newDevice.Name)" />
    </div>
    
    <div class="form-group">
        <label>Device Type:</label>
        <InputSelect @bind-Value="newDevice.Type" class="form-control">
            <option value="@DeviceType.Display">Display</option>
            <option value="@DeviceType.Codec">Codec</option>
            <option value="@DeviceType.Camera">Camera</option>
        </InputSelect>
    </div>
    
    <button type="submit" class="btn btn-primary">Save</button>
</EditForm>

@code {
    private Device newDevice = new()
    {
        Id = Guid.NewGuid().ToString(),
        Status = DeviceStatus.Offline,
        Firmware = "v1.0.0"
    };

    private async Task SaveDevice()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        db.Devices.Add(newDevice);
        await db.SaveChangesAsync();
        
        Navigation.NavigateTo("/devices");
    }
}
```

### Pattern 3: Edit Form with Parameter

```razor
@page "/devices/edit/{DeviceId}"
@inject IDbContextFactory<AppDbContext> DbFactory
@inject NavigationManager Navigation

@if (device == null)
{
    <p><em>Loading...</em></p>
}
else
{
    <h2>Edit @device.Name</h2>
    
    <EditForm Model="device" OnValidSubmit="SaveChanges">
        <div class="form-group">
            <label>Name:</label>
            <InputText @bind-Value="device.Name" class="form-control" />
        </div>
        
        <div class="form-group">
            <label>Status:</label>
            <InputSelect @bind-Value="device.Status" class="form-control">
                <option value="@DeviceStatus.Online">Online</option>
                <option value="@DeviceStatus.Offline">Offline</option>
            </InputSelect>
        </div>
        
        <button type="submit" class="btn btn-primary">Save</button>
        <button type="button" class="btn btn-secondary" 
                @onclick="Cancel">Cancel</button>
    </EditForm>
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
        
        Navigation.NavigateTo("/devices");
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/devices");
    }
}
```

### Pattern 4: Delete with Confirmation

```razor
@page "/devices"
@inject IDbContextFactory<AppDbContext> DbFactory
@inject IJSRuntime JS

<h1>Devices</h1>

<table class="table">
    <thead>
        <tr><th>Name</th><th>Status</th><th>Actions</th></tr>
    </thead>
    <tbody>
        @foreach (var device in devices)
        {
            <tr>
                <td>@device.Name</td>
                <td>@device.Status</td>
                <td>
                    <button class="btn btn-sm btn-primary" 
                            @onclick="() => Edit(device.Id)">
                        Edit
                    </button>
                    <button class="btn btn-sm btn-danger" 
                            @onclick="() => Delete(device.Id, device.Name)">
                        Delete
                    </button>
                </td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<Device> devices = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadDevices();
    }

    private async Task LoadDevices()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
    }

    private void Edit(string id)
    {
        Navigation.NavigateTo($"/devices/edit/{id}");
    }

    private async Task Delete(string id, string name)
    {
        bool confirmed = await JS.InvokeAsync<bool>(
            "confirm", 
            $"Are you sure you want to delete {name}?");
        
        if (confirmed)
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var device = await db.Devices.FindAsync(id);
            if (device != null)
            {
                db.Devices.Remove(device);
                await db.SaveChangesAsync();
                await LoadDevices(); // Refresh list
            }
        }
    }
}
```

### Pattern 5: Real-Time Updates with SignalR

```razor
@page "/devices/live"
@inject IDbContextFactory<AppDbContext> DbFactory
@inject NavigationManager Navigation
@implements IAsyncDisposable

<h1>Live Device Monitor</h1>

<div class="row">
    @foreach (var device in devices)
    {
        <div class="col-md-4 mb-3">
            <div class="card @GetCardClass(device)">
                <div class="card-body">
                    <h5 class="card-title">@device.Name</h5>
                    <p class="card-text">
                        Status: @device.Status<br/>
                        Last Seen: @device.LastSeenUtc?.ToString("g")
                    </p>
                </div>
            </div>
        </div>
    }
</div>

@code {
    private List<Device> devices = new();
    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        // Load initial data from database
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();

        // Connect to SignalR hub for real-time updates
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/controlhub"))
            .WithAutomaticReconnect()
            .Build();

        // Subscribe to device updates
        hubConnection.On<string, DeviceStatus>("DeviceStatusChanged", 
            async (deviceId, status) =>
        {
            var device = devices.FirstOrDefault(d => d.Id == deviceId);
            if (device != null)
            {
                device.Status = status;
                device.LastSeenUtc = DateTime.UtcNow;
                await InvokeAsync(StateHasChanged);
            }
        });

        await hubConnection.StartAsync();
    }

    private string GetCardClass(Device device) => device.Status switch
    {
        DeviceStatus.Online => "border-success",
        DeviceStatus.Offline => "border-danger",
        _ => "border-secondary"
    };

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

### Pattern 6: Search/Filter

```razor
@page "/devices/search"
@inject IDbContextFactory<AppDbContext> DbFactory

<h1>Search Devices</h1>

<div class="form-group">
    <input type="text" class="form-control" placeholder="Search by name..."
           @bind="searchTerm" @bind:event="oninput" />
</div>

<div class="form-group">
    <label>Filter by Status:</label>
    <select class="form-control" @bind="statusFilter">
        <option value="">All</option>
        <option value="@DeviceStatus.Online">Online</option>
        <option value="@DeviceStatus.Offline">Offline</option>
    </select>
</div>

<button class="btn btn-primary" @onclick="Search">Search</button>

<table class="table mt-3">
    <thead>
        <tr><th>Name</th><th>Status</th><th>Type</th></tr>
    </thead>
    <tbody>
        @foreach (var device in filteredDevices)
        {
            <tr>
                <td>@device.Name</td>
                <td>@device.Status</td>
                <td>@device.Type</td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<Device> filteredDevices = new();
    private string searchTerm = "";
    private string statusFilter = "";

    protected override async Task OnInitializedAsync()
    {
        await Search();
    }

    private async Task Search()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        
        IQueryable<Device> query = db.Devices;

        // Apply search term
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(d => d.Name.Contains(searchTerm) 
                                  || d.Location.Contains(searchTerm));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var status = Enum.Parse<DeviceStatus>(statusFilter);
            query = query.Where(d => d.Status == status);
        }

        filteredDevices = await query
            .OrderBy(d => d.Name)
            .ToListAsync();
    }
}
```

---

## Lifecycle Methods

### Component Lifecycle Order

```csharp
1. SetParametersAsync()      // Parameters set
2. OnInitialized()            // Component initialized (sync)
3. OnInitializedAsync()       // Component initialized (async) ? DATABASE QUERY HERE
4. OnParametersSet()          // After parameters set (sync)
5. OnParametersSetAsync()     // After parameters set (async)
6. OnAfterRender()            // After component rendered (sync)
7. OnAfterRenderAsync()       // After component rendered (async)
```

**Best Practice**: Use `OnInitializedAsync()` for initial database queries.

---

## DbContext Lifetime Management

### ? CORRECT: Factory Pattern

```csharp
// In component
await using var db = await DbFactory.CreateDbContextAsync();
var devices = await db.Devices.ToListAsync();
// DbContext automatically disposed here
```

### ? CORRECT: Component Lifetime (for multiple operations)

```csharp
@implements IAsyncDisposable

@code {
    private AppDbContext? db;

    protected override async Task OnInitializedAsync()
    {
        db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
    }

    private async Task UpdateDevice(string id)
    {
        var device = await db!.Devices.FindAsync(id);
        device.Status = DeviceStatus.Online;
        await db.SaveChangesAsync();
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

### ? WRONG: Singleton DbContext

```csharp
// DON'T DO THIS - causes threading issues
builder.Services.AddDbContext<AppDbContext>(ServiceLifetime.Singleton);
```

---

## Performance Tips

### 1. Use AsNoTracking for Read-Only Queries

```csharp
// Faster, less memory
var devices = await db.Devices
    .AsNoTracking()
    .ToListAsync();
```

### 2. Async All The Way

```csharp
// ? WRONG: Blocks SignalR thread
var devices = db.Devices.ToList();

// ? CORRECT: Non-blocking
var devices = await db.Devices.ToListAsync();
```

### 3. Project Only Needed Columns

```csharp
// Only select what you need
var deviceNames = await db.Devices
    .Select(d => new { d.Id, d.Name, d.Status })
    .ToListAsync();
```

### 4. Pagination

```csharp
private int pageSize = 10;
private int currentPage = 0;

private async Task LoadPage()
{
    await using var db = await DbFactory.CreateDbContextAsync();
    devices = await db.Devices
        .OrderBy(d => d.Name)
        .Skip(currentPage * pageSize)
        .Take(pageSize)
        .ToListAsync();
}
```

---

## Common Pitfalls

### Pitfall 1: Forgetting Async/Await

```csharp
// ? WRONG: Doesn't wait for database
protected override void OnInitialized()
{
    LoadDevices(); // Fire and forget
}

// ? CORRECT
protected override async Task OnInitializedAsync()
{
    await LoadDevices();
}
```

### Pitfall 2: Capturing DbContext in Event Handlers

```csharp
// ? WRONG: DbContext may be disposed
protected override async Task OnInitializedAsync()
{
    db = await DbFactory.CreateDbContextAsync();
    timer = new Timer(async _ =>
    {
        // db might be disposed here!
        devices = await db.Devices.ToListAsync();
    }, null, 0, 5000);
}

// ? CORRECT: Create new DbContext each time
protected override async Task OnInitializedAsync()
{
    timer = new Timer(async _ =>
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        devices = await db.Devices.ToListAsync();
        await InvokeAsync(StateHasChanged);
    }, null, 0, 5000);
}
```

### Pitfall 3: Not Calling StateHasChanged

```csharp
// ? WRONG: UI won't update
private async Task RefreshDevices()
{
    await using var db = await DbFactory.CreateDbContextAsync();
    devices = await db.Devices.ToListAsync();
    // UI doesn't know data changed
}

// ? CORRECT
private async Task RefreshDevices()
{
    await using var db = await DbFactory.CreateDbContextAsync();
    devices = await db.Devices.ToListAsync();
    StateHasChanged(); // Or await InvokeAsync(StateHasChanged);
}
```

---

## Debugging

### Enable EF Core SQL Logging

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Console Output:**
```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (2ms) [Parameters=[], CommandType='Text']
      SELECT "d"."Id", "d"."Name", "d"."Status" FROM "Devices" AS "d"
```

### Breakpoint Locations

```csharp
protected override async Task OnInitializedAsync()
{
    await using var db = await DbFactory.CreateDbContextAsync();
    
    // ?? Set breakpoint here
    var devices = await db.Devices.ToListAsync();
    
    // Check devices.Count in debugger
    this.devices = devices;
}
```

---

## Quick Checklist

? Inject `IDbContextFactory<AppDbContext>`, not `AppDbContext` directly  
? Use `await using var db = await DbFactory.CreateDbContextAsync()`  
? Query in `OnInitializedAsync()` for initial load  
? Always use `async`/`await` with EF Core  
? Call `StateHasChanged()` after data updates  
? Use `AsNoTracking()` for read-only queries  
? Dispose DbContext with `await using` or `IAsyncDisposable`  
? Handle null checks for parameters and async loading states  
? Use `@if (data == null)` to show loading indicators  

---

## Summary

**Remember**: In Blazor Server, your Razor components run on the server. The browser is just displaying HTML. All database access happens server-side via dependency injection and Entity Framework Core.

```
Your Component (@code block)
  ? [Dependency Injection]
IDbContextFactory<AppDbContext>
  ? [CreateDbContextAsync()]
AppDbContext
  ? [ToListAsync()]
Entity Framework Core
  ? [SQL Query]
SQLite Database
```

The browser never sees your C# code, connection strings, or SQL queries. It only receives the rendered HTML via SignalR.
