# Cyviz Integration Tests

This project contains integration tests for the Cyviz application using `WebApplicationFactory`.

## Overview

The test suite includes:

- **API Tests** - Testing DevicesController endpoints
- **Authentication Tests** - Testing API key middleware
- **Database Tests** - Testing EF Core operations with in-memory database
- **Blazor Tests** - Testing Blazor page rendering
- **Health Check Tests** - Testing health endpoints

## Running Tests

### Visual Studio
1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All Tests"

### Command Line
```bash
dotnet test
```

### With Coverage
```bash
dotnet test /p:CollectCoverage=true
```

## Test Structure

### WebApplicationFactory
`CyvizWebApplicationFactory` configures:
- In-memory database (isolated per test run)
- Test-specific services
- Background services disabled for deterministic tests
- Seeded test data

### Test Categories

#### DevicesControllerTests
- GET /api/devices - List devices with filtering/pagination
- GET /api/devices/{id} - Get device details
- PUT /api/devices/{id} - Update device
- POST /api/devices/{id}/commands - Send command
- GET /api/devices/{id}/commands/{commandId} - Get command status
- POST /api/devices/{id}/heartbeat - Update device heartbeat

#### ApiKeyAuthenticationTests
- Validates API key middleware behavior
- Tests authorized/unauthorized access

#### DatabaseIntegrationTests
- Direct database operations
- Entity relationships
- Constraint validation
- Value converters (e.g., string[] to JSON)

#### BlazorPageTests
- Page rendering
- Static file serving
- Hub endpoint accessibility

## Test Data

Default test devices:
- `device-01` - TestDisplay-1 (Online, Display)
- `device-02` - TestCodec-1 (Offline, Codec)
- `device-03` - TestSwitcher-1 (Online, Switcher)

## Key Features

### Isolated Database
Each test run uses a unique in-memory database:
```csharp
options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
```

### API Key Header
Most tests include the required API key:
```csharp
_client.DefaultRequestHeaders.Add("X-API-Key", "local-dev-key");
```

### FluentAssertions
Clean, readable assertions:
```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
result.Items.Should().HaveCount(3);
```

## Adding New Tests

1. Create a new test class implementing `IClassFixture<CyvizWebApplicationFactory>`
2. Inject the factory in the constructor
3. Create test methods with `[Fact]` or `[Theory]` attributes
4. Use FluentAssertions for readable assertions

Example:
```csharp
public class NewFeatureTests : IClassFixture<CyvizWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NewFeatureTests(CyvizWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", "local-dev-key");
    }

    [Fact]
    public async Task NewEndpoint_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/new");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## Notes

- Background services (CommandRouter, DeviceStatusMonitor) are disabled during tests
- Each test class shares the same WebApplicationFactory instance (faster)
- Database is recreated for each test run (isolated)
- Use `CreateScope()` for direct database access in tests

## Troubleshooting

### Tests fail with "Program" not found
Make Program class public in Program.cs or add to Cyviz.csproj:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="Cyviz.IntegrationTests" />
</ItemGroup>
```

### Database errors
Check that AppDbContext is properly registered in test services and migrations/model are compatible with in-memory provider.

### SignalR tests fail
SignalR requires WebSocket support. For full SignalR testing, consider using Microsoft.AspNetCore.SignalR.Client with actual connections.
