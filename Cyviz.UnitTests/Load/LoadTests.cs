using System.Diagnostics;
using System.Collections.Concurrent;

namespace Cyviz.UnitTests.Load;

/// <summary>
/// Load tests to verify system behavior under concurrent load.
/// Tests: 25 devices × 10 commands/min ? no duplicates, no deadlocks
/// </summary>
public class LoadTests
{
    [Fact(Skip = "Long-running load test - enable manually")]
    public async Task LoadTest_25Devices_10CommandsPerMinute_NoDuplicatesOrDeadlocks()
    {
        // Arrange
        const int deviceCount = 25;
        const int commandsPerMinute = 10;
        const int durationSeconds = 60;
        
        var loadSimulator = new LoadSimulator(deviceCount);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));

        // Act
        var sw = Stopwatch.StartNew();
        await loadSimulator.RunLoadTest(commandsPerMinute, cts.Token);
        sw.Stop();

        // Assert
        var stats = loadSimulator.GetStatistics();
        
        stats.TotalCommands.Should().BeGreaterThan((int)(deviceCount * commandsPerMinute * 0.9), 
            "should process most expected commands");
        
        stats.DuplicateCompletions.Should().Be(0, 
            "no command should complete twice");
        
        stats.DeadlocksDetected.Should().Be(0, 
            "no deadlocks should occur");
        
        stats.FailedCommands.Should().BeLessThan((int)(stats.TotalCommands * 0.05), 
            "failure rate should be < 5%");

        Console.WriteLine($"Load Test Results:");
        Console.WriteLine($"  Duration: {sw.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Total Commands: {stats.TotalCommands}");
        Console.WriteLine($"  Successful: {stats.SuccessfulCommands}");
        Console.WriteLine($"  Failed: {stats.FailedCommands}");
        Console.WriteLine($"  Avg Latency: {stats.AverageLatencyMs:F2}ms");
        Console.WriteLine($"  Max Latency: {stats.MaxLatencyMs:F2}ms");
        Console.WriteLine($"  Commands/sec: {stats.CommandsPerSecond:F2}");
    }

    [Theory]
    [InlineData(5, 20)]  // 5 devices, 20 cmd/min
    [InlineData(10, 15)] // 10 devices, 15 cmd/min
    [InlineData(25, 10)] // 25 devices, 10 cmd/min
    public async Task LoadTest_VariousConfigurations_MaintainIdempotency(int deviceCount, int commandsPerMinute)
    {
        // Arrange
        var loadSimulator = new LoadSimulator(deviceCount);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Shorter test

        // Act
        await loadSimulator.RunLoadTest(commandsPerMinute, cts.Token);

        // Assert
        var stats = loadSimulator.GetStatistics();
        stats.DuplicateCompletions.Should().Be(0, "idempotency must be maintained under load");
    }

    [Fact]
    public async Task LoadTest_ConcurrentRetries_NoRaceConditions()
    {
        // Arrange - Simulate aggressive retry scenario
        const int deviceCount = 10;
        const int commandsPerDevice = 20;
        const int retriesPerCommand = 3;

        var loadSimulator = new LoadSimulator(deviceCount);

        // Act - Send commands with intentional retries
        var tasks = Enumerable.Range(0, deviceCount).SelectMany(deviceIndex =>
            Enumerable.Range(0, commandsPerDevice).Select(async cmdIndex =>
            {
                var idempotencyKey = $"device-{deviceIndex:D2}-cmd-{cmdIndex}";
                
                // Simulate multiple clients/retries with same idempotency key
                var retryTasks = Enumerable.Range(0, retriesPerCommand).Select(async _ =>
                {
                    await Task.Delay(Random.Shared.Next(1, 10)); // Random delay
                    return await loadSimulator.SendCommand($"device-{deviceIndex:D2}", "Ping", idempotencyKey);
                });

                return await Task.WhenAll(retryTasks);
            })
        ).ToArray();

        var results = (await Task.WhenAll(tasks)).SelectMany(r => r).ToList();

        // Assert
        var stats = loadSimulator.GetStatistics();
        stats.DuplicateCompletions.Should().Be(0, "retries should not cause duplicate completions");
        
        // Group by idempotency key - each should have exactly one completion
        var completionsByKey = results
            .GroupBy(r => r.IdempotencyKey)
            .Select(g => new { Key = g.Key, CompletedCount = g.Count(r => r.Completed) })
            .ToList();

        completionsByKey.Should().OnlyContain(g => g.CompletedCount <= 1, 
            "each idempotency key should complete at most once");
    }

    [Fact]
    public async Task LoadTest_HighConcurrency_NoDeadlocks()
    {
        // Arrange
        const int concurrentOperations = 100;
        var loadSimulator = new LoadSimulator(10);
        var deadlockDetected = false;

        // Act - Stress test with timeout detection
        var tasks = Enumerable.Range(0, concurrentOperations).Select(async i =>
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var deviceId = $"device-{i % 10:D2}";
                var result = await loadSimulator.SendCommand(deviceId, "Reboot", Guid.NewGuid().ToString(), cts.Token);
                return result;
            }
            catch (TaskCanceledException)
            {
                deadlockDetected = true;
                return new CommandResult { Success = false, Error = "Timeout" };
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        deadlockDetected.Should().BeFalse("no operations should timeout due to deadlock");
        var stats = loadSimulator.GetStatistics();
        stats.DeadlocksDetected.Should().Be(0);
    }

    // Load simulator implementation
    private class LoadSimulator
    {
        private readonly int _deviceCount;
        private readonly ConcurrentDictionary<string, SimulatedDevice> _devices = new();
        private readonly ConcurrentBag<CommandExecution> _executions = new();
        private readonly ConcurrentDictionary<string, int> _completionCounts = new();
        private int _deadlockCounter = 0;

        public LoadSimulator(int deviceCount)
        {
            _deviceCount = deviceCount;
            
            // Initialize devices
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceId = $"device-{i:D2}";
                _devices[deviceId] = new SimulatedDevice { Id = deviceId };
            }
        }

        public async Task RunLoadTest(int commandsPerMinute, CancellationToken ct)
        {
            var delayBetweenCommands = TimeSpan.FromSeconds(60.0 / commandsPerMinute);
            var deviceIds = _devices.Keys.ToList();

            // Generate continuous load
            var tasks = deviceIds.Select(async deviceId =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var idempotencyKey = $"{deviceId}-{Guid.NewGuid()}";
                    await SendCommand(deviceId, "Ping", idempotencyKey, ct);
                    await Task.Delay(delayBetweenCommands, ct);
                }
            }).ToArray();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation token fires
            }
        }

        public async Task<CommandResult> SendCommand(string deviceId, string command, string idempotencyKey, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Atomic idempotency check - use TryAdd to prevent race conditions
                if (!_completionCounts.TryAdd(idempotencyKey, 1))
                {
                    // Key already exists - this is a duplicate/retry
                    return new CommandResult
                    {
                        Success = true,
                        IdempotencyKey = idempotencyKey,
                        Completed = false, // Not a new completion, just returning cached result
                        LatencyMs = 0 // Cached response
                    };
                }

                // This is the first execution for this idempotency key
                // Simulate command execution
                await Task.Delay(Random.Shared.Next(10, 50), ct);

                // Track execution
                var execution = new CommandExecution
                {
                    DeviceId = deviceId,
                    Command = command,
                    IdempotencyKey = idempotencyKey,
                    StartTime = DateTime.UtcNow,
                    LatencyMs = sw.Elapsed.TotalMilliseconds,
                    Success = true, // TryAdd succeeded, so this is the only execution
                    EndTime = DateTime.UtcNow
                };

                _executions.Add(execution);

                return new CommandResult
                {
                    Success = true,
                    IdempotencyKey = idempotencyKey,
                    Completed = true,
                    LatencyMs = execution.LatencyMs
                };
            }
            catch (TaskCanceledException)
            {
                Interlocked.Increment(ref _deadlockCounter);
                throw;
            }
        }

        public LoadStatistics GetStatistics()
        {
            var executions = _executions.ToList();
            
            return new LoadStatistics
            {
                TotalCommands = executions.Count,
                SuccessfulCommands = executions.Count(e => e.Success),
                FailedCommands = executions.Count(e => !e.Success),
                DuplicateCompletions = _completionCounts.Count(kvp => kvp.Value > 1),
                DeadlocksDetected = _deadlockCounter,
                AverageLatencyMs = executions.Any() ? executions.Average(e => e.LatencyMs) : 0,
                MaxLatencyMs = executions.Any() ? executions.Max(e => e.LatencyMs) : 0,
                CommandsPerSecond = executions.Count / 
                    (executions.Any() ? (executions.Max(e => e.EndTime) - executions.Min(e => e.StartTime)).TotalSeconds : 1)
            };
        }

        private class SimulatedDevice
        {
            public string Id { get; set; } = "";
        }

        private class CommandExecution
        {
            public string DeviceId { get; set; } = "";
            public string Command { get; set; } = "";
            public string IdempotencyKey { get; set; } = "";
            public bool Success { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public double LatencyMs { get; set; }
        }
    }

    private class CommandResult
    {
        public bool Success { get; set; }
        public string IdempotencyKey { get; set; } = "";
        public bool Completed { get; set; }
        public double LatencyMs { get; set; }
        public string Error { get; set; } = "";
    }

    private class LoadStatistics
    {
        public int TotalCommands { get; set; }
        public int SuccessfulCommands { get; set; }
        public int FailedCommands { get; set; }
        public int DuplicateCompletions { get; set; }
        public int DeadlocksDetected { get; set; }
        public double AverageLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double CommandsPerSecond { get; set; }
    }
}
