# ??? Device Control Dashboard

## Overview

A fully responsive Blazor Server dashboard for managing and monitoring IoT devices in real-time at **http://localhost:3000**.

## Features

### 1. ?? Devices Table
- **Filters**: Search by name/location, filter by status (Online/Offline), filter by type (Display/Codec/Switcher/Sensor)
- **Sorting**: Click column headers to sort (Name, Type)
- **Keyset Pagination**: Navigate through pages with Previous/Next buttons
- **Live Status Updates**: Real-time status changes via SignalR
- **Visual Indicators**: Color-coded status badges and device type icons
- **Row Selection**: Click any row to open device details

### 2. ?? Device Detail Drawer
Opens on the right side when you click on a device:

#### Device Information
- Full device details (ID, Type, Status, Protocol, Location, Firmware, Last Seen)
- Capabilities badges
- Real-time status indicator

#### ?? Live Telemetry Chart
- **SVG-based line chart** (last minute of data)
- Three metrics tracked:
  - ??? Temperature ( C)
  - ?? CPU Usage (%)
  - ?? Memory Usage (%)
- Auto-refreshes every 2 seconds
- Current values displayed below chart
- Responsive and animated

#### ? Command Form
- **Command Selection**: Choose from device capabilities or standard commands
- **Idempotency Key**: 
  - Automatically generated unique key
  - Visible and copyable
  - "New Key" button to regenerate
  - "Send Again" button to retry with same key (safe retries)
- **Command Results**:
  - ? Success/? Failure indicator
  - Latency in milliseconds
  - Correlation ID for tracking
  - Idempotency Key used
  - Full result/error message
  - Timestamp

#### ?? Recent Commands History
- Last 10 commands for the device
- Status indicators (? Completed, ? Failed, ? Pending)
- Latency information
- Truncated idempotency keys
- Error messages for failed commands
- Auto-refreshes with SignalR

### 3. ?? Toast Notifications
- **Status Changes**: Device online/offline notifications
- **Command Results**: Success/failure alerts with details
- **Connection Status**: SignalR reconnection notifications
- **Auto-dismiss**: Notifications disappear after 5 seconds
- **Manual close**: Click   to dismiss
- **Color-coded**:
  - ?? Green: Success
  - ?? Red: Error
  - ?? Yellow: Warning
  - ?? Blue: Info

## Access

### URLs
- **HTTP**: http://localhost:3000
- **HTTPS**: https://localhost:3001--discarted dut to trusted certs and test enviroment
- **Dashboard**: http://localhost:3000/dashboard

### Navigation
- Click "Dashboard" in the main navigation menu
- Or navigate directly to `/dashboard`

## How to Use

### Viewing Devices
1. Navigate to the dashboard
2. Use filters to find specific devices:
   - Type in search box for name/location
   - Select status filter (All/Online/Offline)
   - Select type filter (All/Display/Codec/Switcher/Sensor)
3. Click "Apply Filters" or press Enter
4. Click "Reset" to clear all filters

### Sorting & Pagination
- Click column headers (Name, Type) to sort
- Click again to reverse sort order
- Use Previous/Next buttons to navigate pages
- Page size: 10 devices per page

### Viewing Device Details
1. Click any device row in the table
2. Drawer opens on the right side
3. View device info, telemetry, and send commands
4. Click   or overlay to close drawer

### Sending Commands
1. Open device detail drawer
2. Select a command from dropdown
3. Note the auto-generated idempotency key
4. Click "Send Command"
5. View result below form (latency, correlation ID, etc.)
6. Use "Send Again" to safely retry with same key
7. Click "New Key" to generate a new idempotency key

### Monitoring Telemetry
- Telemetry chart auto-updates every 2 seconds
- Shows last minute of data
- Three lines: Temperature (red), CPU (blue), Memory (green)
- Current values displayed below chart
- Click "Refresh" to manually update

## Technical Details

### Components Structure
```
Dashboard.razor (Main page)
??? Filters Section
??? Devices Table
?   ??? Status badges
?   ??? Sortable columns
?   ??? Keyset pagination
??? DeviceDetailDrawer.razor
?   ??? Device info section
?   ??? TelemetryChart.razor (SVG chart)
?   ??? Command form
?   ??? Command history
??? Toast notifications
```

### Real-Time Updates (SignalR)
- **Device Status**: `DeviceStatusChanged` event
- **Command Completion**: `CommandCompleted` event
- **Telemetry**: `DeviceTelemetryReceived` event
- **Auto-reconnect**: Handles connection drops gracefully

### Styling
- **Responsive design**: Works on desktop, tablet, and mobile
- **Modern UI**: Gradient backgrounds, smooth animations
- **Color-coded**: Intuitive status indicators
- **Dark mode ready**: Professional color scheme

### Performance
- **Efficient pagination**: Only loads visible page
- **Lazy loading**: Drawer loads data on demand
- **Optimized SignalR**: Minimal data transfer
- **Debounced filters**: Smooth user experience

## Code Examples

### Accessing Device Data
```csharp
await using var db = await DbFactory.CreateDbContextAsync();
var devices = await db.Devices
    .Where(d => d.Status == DeviceStatus.Online)
    .OrderBy(d => d.Name)
    .ToListAsync();
```

### Sending Commands
```csharp
var command = new DeviceCommand
{
    Id = Guid.NewGuid(),
    DeviceId = device.Id,
    Command = "POWER_ON",
    IdempotencyKey = Guid.NewGuid().ToString(),
    CreatedUtc = DateTime.UtcNow,
    Status = "Pending"
};

db.Commands.Add(command);
await db.SaveChangesAsync();
```

### SignalR Connection
```csharp
hubConnection = new HubConnectionBuilder()
    .WithUrl(Navigation.ToAbsoluteUri("/controlhub"))
    .WithAutomaticReconnect()
    .Build();

hubConnection.On<string, DeviceStatus>("DeviceStatusChanged", 
    async (deviceId, status) => {
        // Handle status change
    });

await hubConnection.StartAsync();
```

## Files Created

### Pages
- `Cyviz/Pages/Dashboard.razor` - Main dashboard page

### Components
- `Cyviz/Components/DeviceDetailDrawer.razor` - Device details drawer
- `Cyviz/Components/TelemetryChart.razor` - SVG telemetry chart

### Styles
- `Cyviz/wwwroot/css/dashboard.css` - Complete dashboard styling

### Updates
- `Cyviz/Pages/_Layout.cshtml` - Added dashboard CSS link
- `Cyviz/Shared/NavMenu.razor` - Added dashboard navigation link
- `Cyviz/_Imports.razor` - Added required using directives
- `Cyviz/GlobalUsings.cs` - Added SignalR client using
- `Cyviz/Program.cs` - Set port to 3000

## Troubleshooting

### Dashboard not loading
- Check if application is running on port 3000
- Verify database is seeded with devices
- Check browser console for errors

### Real-time updates not working
- Verify SignalR connection (green indicator in header)
- Check if EdgeSimulator is running
- Look for connection errors in browser console

### Commands not executing
- Ensure device is online
- Check CommandRouter is running
- Verify idempotency key is unique

### Telemetry not showing
- Wait for edge simulator to generate data
- Check if DeviceStatusMonitor is running
- Verify device has sent telemetry in last minute

## Browser Support
- ? Chrome/Edge (Recommended)
- ? Firefox
- ? Safari
- ? Mobile browsers

## Next Steps
1. Start the application: `dotnet run`
2. Navigate to http://localhost:3000/dashboard
3. Explore devices and send commands
4. Watch real-time updates in action!

---

**Built with ?? using Blazor Server, SignalR, Entity Framework Core, and SQLite**
