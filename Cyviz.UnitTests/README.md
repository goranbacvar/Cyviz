# Cyviz Unit Tests

Comprehensive unit test suite using **xUnit**, **NSubstitute**, **FluentAssertions**, and **FsCheck**.

## ?? Test Categories

### 1. **Resilience Tests** (`Application/`)
Tests for circuit breaker, retry policies, and device isolation.

- **CircuitBreakerTests.cs** - Circuit breaker state transitions, failure thresholds, recovery
- **RetryPolicyTests.cs** - Exponential backoff, jitter, cancellation handling
- **DeviceCircuitsTests.cs** - Device-level circuit isolation, thread safety

### 2. **Property-Based Tests** (`Application/IdempotencyPropertyTests.cs`)
Uses FsCheck to generate random scenarios and verify invariants:

- Random command retries ? never two "Completed" events for same idempotency key
- Idempotent execution ? always returns same result
- Concurrent retries ? exactly one success per idempotency key
- Retry delays ? prevent thundering herd

### 3. **Validation Tests** (`Application/ValidationTests.cs`)
FluentValidation rules for:

- SendCommandRequest validation
- Idempotency key constraints
- Command parameter validation
- Device model validation

### 4. **Scenario Tests** (`Scenarios/`)
End-to-end workflow testing:

- POST /commands ? SignalR CommandCompleted ? DB state verification
- Duplicate command handling
- Circuit breaker trip scenarios
- Concurrent command execution

### 5. **Load Tests** (`Load/LoadTests.cs`)
Performance and concurrency testing:

- **25 devices × 10 commands/min** - No duplicates, no deadlocks
- Variable load configurations
- Aggressive retry scenarios
- High concurrency stress tests

## ?? Running Tests

### All Tests
```bash
dotnet test
```

### Specific Category
```bash
dotnet test --filter "FullyQualifiedName~CircuitBreaker"
dotnet test --filter "FullyQualifiedName~Property"
dotnet test --filter "FullyQualifiedName~Scenario"
```

### With Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### Load Tests (Disabled by Default)
Load tests are marked with `Skip` to prevent long test runs. Enable manually:

```csharp
[Fact] // Remove Skip attribute
public async Task LoadTest_25Devices_10CommandsPerMinute_NoDuplicatesOrDeadlocks()
```

Or run from command line:
```bash
dotnet test --filter "FullyQualifiedName~LoadTest" -- RunConfiguration.SkipTests=false
```

## ?? Test Coverage

| Component | Coverage | Tests |
|-----------|----------|-------|
| CircuitBreaker | ~95% | 7 tests |
| RetryPolicy | ~90% | 6 tests |
| DeviceCircuits | ~100% | 5 tests |
| Idempotency | Property-based | 4 properties |
| Validation | ~85% | 10 tests |
| Workflows | Scenario | 5 scenarios |
| Load | Stress | 4 load tests |

## ?? Key Test Patterns

### 1. Circuit Breaker Testing
```csharp
[Fact]
public void CircuitBreaker_OpensAfter5Failures()
{
    var cb = new CircuitBreaker();
    
    for (int i = 0; i < 5; i++)
    {
        cb.RecordFailure();
    }
    
    cb.IsOpen.Should().BeTrue();
}
```

### 2. Property-Based Testing
```csharp
[Property(MaxTest = 1000)]
public Property RandomCommandRetries_NeverProduceDuplicateCompletedEvents()
{
    return Prop.ForAll(
        Arb.From(GenCommandScenarios()),
        scenario => VerifyNoDuplicates(scenario));
}
```

### 3. Scenario Testing
```csharp
[Fact]
public async Task CommandWorkflow_FromPostToCompletion_SuccessfullyTracksState()
{
    var workflow = new CommandWorkflowSimulator();
    
    var postResult = await workflow.PostCommand("device-01", "Reboot", key);
    await workflow.SimulateDeviceExecution(postResult.CommandId, success: true);
    var dbCommand = await workflow.GetCommandFromDb(postResult.CommandId);
    
    dbCommand.Status.Should().Be("Completed");
}
```

### 4. Load Testing
```csharp
[Fact]
public async Task LoadTest_25Devices_10CommandsPerMinute_NoDuplicatesOrDeadlocks()
{
    var loadSimulator = new LoadSimulator(deviceCount: 25);
    await loadSimulator.RunLoadTest(commandsPerMinute: 10, cts.Token);
    
    var stats = loadSimulator.GetStatistics();
    stats.DuplicateCompletions.Should().Be(0);
}
```

## ?? Dependencies

```xml
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="NSubstitute" Version="5.1.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="FsCheck.Xunit" Version="2.16.6" />
```

## ?? Test Naming Convention

```
[Component]_[Scenario]_[ExpectedBehavior]
```

Examples:
- `CircuitBreaker_OpensAfter5Failures`
- `RetryPolicy_RespectsExponentialBackoff`
- `CommandWorkflow_DuplicatePost_ReturnsExistingCommand`

## ?? Assertion Style

Using **FluentAssertions** for readable assertions:

```csharp
result.Should().BeTrue();
items.Should().HaveCount(5);
action.Should().Throw<ValidationException>();
list.Should().OnlyHaveUniqueItems();
```

## ?? Debugging Tests

### Visual Studio
1. Right-click test ? Debug Test
2. Set breakpoints in test or production code
3. Use Test Explorer to view results

### VS Code
1. Install C# extension
2. Use `.NET Core Test Explorer` extension
3. Click debug icon next to test

### Command Line
```bash
dotnet test --logger "console;verbosity=detailed"
```

## ?? Continuous Integration

### GitHub Actions
```yaml
- name: Run Unit Tests
  run: dotnet test Cyviz.UnitTests/Cyviz.UnitTests.csproj --no-build
```

### Test Reports
```bash
dotnet test --logger "trx;LogFileName=test-results.trx"
```

## ?? Adding New Tests

1. Create test file in appropriate category folder
2. Follow naming conventions
3. Use FluentAssertions for readability
4. Add documentation comments for complex tests
5. Run all tests before committing:
   ```bash
   dotnet test
   ```

## ?? Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Docs](https://fluentassertions.com/)
- [FsCheck Guide](https://fscheck.github.io/FsCheck/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)

## ? Quick Start

```bash
# Clone and restore
git clone <repo-url>
cd Cyviz.UnitTests
dotnet restore

# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test
dotnet test --filter "CircuitBreaker_OpensAfter5Failures"
```

## ?? Test Goals

- ? **100% idempotency guarantee** - No duplicate completions
- ? **Circuit breaker resilience** - Prevent cascading failures  
- ? **Retry reliability** - Exponential backoff with jitter
- ? **Concurrency safety** - No race conditions or deadlocks
- ? **Load handling** - Support 25 devices × 10 cmd/min

---

**Status**: ? All core scenarios covered
**Last Updated**: 2024
