# ?? Quick Start: Dashboard

## Start the Application

### Option 1: Visual Studio
1. Press `F5` or click "Start Debugging"
2. Browser will automatically open to http://localhost:3000/dashboard

### Option 2: Command Line
```bash
cd Cyviz
dotnet run
```
Then open browser to: **http://localhost:3000/dashboard**

### Option 3: Visual Studio Code
```bash
cd Cyviz
dotnet watch run
```
Navigate to: **http://localhost:3000/dashboard**

## What You'll See

### Initial State
- Dashboard with 20 seeded devices
- Connection status indicator (?? Connected)
- Device count display
- Filters section (Search, Status, Type)
- Paginated table (10 devices per page)

### Real-Time Features
1. **Device Status Updates**
   - Watch devices go online/offline
   - Toast notifications appear
   - Table updates automatically

2. **Live Telemetry**
   - Click any device row
   - Drawer opens on right
   - Chart shows live data (updates every 2s)

3. **Command Execution**
   - Select a command (e.g., POWER_ON)
   - Click "Send Command"
   - See result with latency
   - Toast notification appears

## Test Scenarios

### Test 1: Filter Devices
```
1. Type "Display" in search box
2. Select "Online" in status filter
3. Click "Apply Filters"
4. Result: Only online display devices shown
```

### Test 2: Sort Devices
```
1. Click "Name" column header ? Sort A-Z
2. Click again ? Sort Z-A
3. Click "Type" column ? Sort by device type
```

### Test 3: Pagination
```
1. Click "Next" button
2. See next 10 devices
3. Click "Previous" to go back
4. Page indicator shows current page
```

### Test 4: View Device Details
```
1. Click any device row
2. Drawer slides in from right
3. See device info, telemetry, and command form
4. Click × or overlay to close
```

### Test 5: Send Command
```
1. Open device drawer
2. Select "POWER_ON" command
3. Note the idempotency key (auto-generated)
4. Click "Send Command"
5. Wait for result (shows latency, correlation ID)
6. See toast notification
7. Check command history below
```

### Test 6: Retry Command
```
1. After sending a command
2. Click "Send Again" button
3. Same idempotency key is reused
4. Command executes safely (idempotent)
```

### Test 7: Watch Real-Time Updates
```
1. Open multiple browser tabs
2. Send command in one tab
3. Watch notification appear in other tabs
4. Status changes propagate across all tabs
```

## Expected Behavior

### Device Status Changes
- EdgeSimulator randomly changes device status every 5-10 seconds
- Green badge (?? Online) or red badge (?? Offline)
- Toast notification: "Device [Name]: Status changed: Offline ? Online"

### Telemetry Updates
- Chart refreshes every 2 seconds
- Shows last minute of data
- Three lines: Temperature, CPU, Memory
- Current values displayed below chart

### Command Results
- Latency: Usually 100-500ms
- Correlation ID: Unique GUID for tracking
- Idempotency Key: Can be reused for safe retries
- Status: ? Completed or ? Failed

### Toast Notifications
Examples:
- ? "Command Completed: Display-1: POWER_ON (245ms)"
- ? "Command Failed: Codec-5: INVALID_CMD - Command not supported"
- ?? "Device Display-3: Status changed: Online ? Offline"
- ?? "Reconnected: Connection restored successfully"

## Troubleshooting

### Issue: Dashboard is blank
**Solution**: 
- Check database is seeded: Look for "Device count: 20" in logs
- Refresh browser (Ctrl+F5)
- Check browser console for errors

### Issue: "?? Disconnected" in header
**Solution**:
- Restart application
- Check SignalR logs in console
- Verify port 3000 is not blocked

### Issue: Telemetry chart is empty
**Solution**:
- Wait 30 seconds for edge simulator to generate data
- Click "Refresh" button
- Check if device is online

### Issue: Commands not executing
**Solution**:
- Ensure device is online (status must be ??)
- Check CommandRouter logs
- Try a different command

### Issue: Drawer won't open
**Solution**:
- Click directly on table row (not button)
- Check browser console for errors
- Try refreshing page

## Architecture Flow

### Viewing Data
```
Browser ? SignalR ? Blazor Server ? EF Core ? SQLite
        ? HTML ? Component Render ? Query ? Database
```

### Sending Command
```
User clicks "Send" ? Dashboard.razor ? DeviceDetailDrawer.razor
    ?
  Create DeviceCommand ? Save to SQLite
    ?
  CommandRouter picks up ? Protocol Adapter
    ?
  Edge device executes ? Updates command status
    ?
  SignalR broadcasts ? All connected clients update
```

### Real-Time Updates
```
EdgeSimulator generates telemetry ? DeviceHub.SendAsync()
    ?
SignalR broadcasts to all clients
    ?
Dashboard.razor receives event ? Updates state ? Re-renders
    ?
Toast notification appears
```

## Performance Notes

- **Load time**: ~500ms (20 devices)
- **SignalR latency**: ~10-50ms
- **Command execution**: 100-500ms
- **Telemetry refresh**: Every 2 seconds
- **Status updates**: Real-time (< 100ms)

## Browser Developer Tools

### Check SignalR Connection
1. Open browser DevTools (F12)
2. Go to Console tab
3. Look for: `[SignalR] Connected`
4. Look for: `DeviceStatusChanged` events

### Check Network Traffic
1. Go to Network tab
2. Filter: WS (WebSocket)
3. See SignalR messages in real-time

### Check Component State
1. Go to Blazor DevTools (if available)
2. Inspect component tree
3. See Dashboard ? DeviceDetailDrawer hierarchy

## Next Steps

Once dashboard is running:

1. ? **Explore UI**: Try all filters, sorting, pagination
2. ? **Send Commands**: Test different device capabilities
3. ? **Monitor Telemetry**: Watch live data updates
4. ? **Test Idempotency**: Use "Send Again" feature
5. ? **Multi-Tab Test**: Open multiple tabs, watch sync
6. ? **Mobile View**: Resize browser, test responsive design

## Stopping the Application

### Visual Studio
- Press `Shift+F5` or click "Stop Debugging"

### Command Line
- Press `Ctrl+C` in terminal

---

**Enjoy your real-time dashboard! ??**

For detailed documentation, see: [DASHBOARD-README.md](DASHBOARD-README.md)
