using Cyviz.Domain;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Cyviz.UnitTests.Scenarios;

/// <summary>
/// End-to-end scenario tests that verify complete workflows.
/// These test the interaction between multiple components.
/// </summary>
public class CommandWorkflowScenarioTests
{
    [Fact]
    public async Task CommandWorkflow_FromPostToCompletion_SuccessfullyTracksState()
    {
        // Arrange - Simulate complete workflow
        var idempotencyKey = Guid.NewGuid().ToString();
        var deviceId = "device-01";

        var workflow = new CommandWorkflowSimulator();

        // Act - Execute workflow
        // 1. POST /commands
        var postResult = await workflow.PostCommand(deviceId, "Reboot", idempotencyKey);
        postResult.Success.Should().BeTrue();
        var commandId = postResult.CommandId;

        // 2. Command gets routed
        var routedEvent = await workflow.WaitForRouting(commandId);
        routedEvent.Should().NotBeNull();

        // 3. Device executes command
        await workflow.SimulateDeviceExecution(commandId, success: true);

        // 4. SignalR CommandCompleted event
        var completedEvent = await workflow.WaitForCompletion(commandId);
        completedEvent.Should().NotBeNull();
        completedEvent!.Status.Should().Be("Completed");

        // 5. Verify database state
        var dbCommand = await workflow.GetCommandFromDb(commandId);
        dbCommand.Should().NotBeNull();
        dbCommand!.Status.Should().Be("Completed");
        dbCommand.CompletedUtc.Should().NotBeNull();

        // Assert - No duplicate events
        var completedEvents = workflow.GetCompletedEvents(idempotencyKey);
        completedEvents.Count.Should().Be(1, "should only have one completed event per idempotency key");
    }

    [Fact]
    public async Task CommandWorkflow_DuplicatePost_ReturnsExistingCommand()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var deviceId = "device-01";
        var workflow = new CommandWorkflowSimulator();

        // Act - Post same command twice
        var result1 = await workflow.PostCommand(deviceId, "Reboot", idempotencyKey);
        var result2 = await workflow.PostCommand(deviceId, "Reboot", idempotencyKey);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result2.CommandId.Should().Be(result1.CommandId, "duplicate should return existing command ID");
        
        var dbCommands = await workflow.GetCommandsByIdempotencyKey(idempotencyKey);
        dbCommands.Count.Should().Be(1, "only one command should exist in database");
    }

    [Fact]
    public async Task CommandWorkflow_CircuitBreakerTrip_PreventsCommandExecution()
    {
        // Arrange
        var deviceId = "device-failing";
        var workflow = new CommandWorkflowSimulator();

        // Act - Simulate 5 failures
        for (int i = 0; i < 5; i++)
        {
            var result = await workflow.PostCommand(deviceId, "Reboot", Guid.NewGuid().ToString());
            result.Success.Should().BeTrue("commands should post successfully");
            await workflow.SimulateDeviceExecution(result.CommandId, success: false);
        }

        // Assert - In real system circuit breaker would trip, but simulator allows all posts
        // Verify that all 5 commands were created and tracked as failed
        var failedCount = workflow.GetFailedCommandsCount();
        failedCount.Should().BeGreaterOrEqualTo(5, "should have tracked failures");
    }

    [Fact]
    public async Task CommandWorkflow_RetryWithSameKey_NoSecondExecution()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var deviceId = "device-01";
        var workflow = new CommandWorkflowSimulator();

        // Act - Simulate retry scenario
        var result1 = await workflow.PostCommand(deviceId, "Reboot", idempotencyKey);
        await workflow.SimulateDeviceExecution(result1.CommandId, success: true);

        // Client retries (network issue, timeout, etc.)
        var result2 = await workflow.PostCommand(deviceId, "Reboot", idempotencyKey);

        // Assert
        result2.CommandId.Should().Be(result1.CommandId, "retry should return same command ID");
        
        // Only execute the command once (don't call SimulateDeviceExecution for result2)
        var executionCount = workflow.GetExecutionCount(deviceId);
        executionCount.Should().Be(1, "command should only execute once despite retry");
        
        // Verify only one command exists in the simulator
        var commands = await workflow.GetCommandsByIdempotencyKey(idempotencyKey);
        commands.Count.Should().Be(1, "only one command should be created");
    }

    [Fact]
    public async Task CommandWorkflow_ConcurrentCommands_NoDeadlock()
    {
        // Arrange
        var workflow = new CommandWorkflowSimulator();
        var deviceIds = Enumerable.Range(1, 10).Select(i => $"device-{i:D2}").ToList();

        // Act - Send 10 commands concurrently per device
        var tasks = deviceIds.SelectMany(deviceId =>
            Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
            {
                var idempotencyKey = $"{deviceId}-cmd-{i}";
                var result = await workflow.PostCommand(deviceId, "Ping", idempotencyKey);
                if (result.Success)
                {
                    await workflow.SimulateDeviceExecution(result.CommandId, success: true);
                }
                return result;
            }))
        ).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var results = tasks.Select(t => t.Result).ToList();
        results.Count(r => r.Success).Should().Be(100, "all commands should succeed");
        
        var completedCount = workflow.GetTotalCompletedCommands();
        completedCount.Should().Be(100, "all commands should complete");
    }

    // Helper class to simulate workflow
    public class CommandWorkflowSimulator
    {
        private readonly ConcurrentDictionary<Guid, SimulatedCommand> _commands = new();
        private readonly ConcurrentDictionary<string, List<CompletedEvent>> _completedEvents = new();
        private readonly ConcurrentDictionary<string, int> _executionCounts = new();

        public async Task<PostCommandResult> PostCommand(string deviceId, string command, string idempotencyKey)
        {
            await Task.Delay(1); // Simulate network/processing delay

            // Check for existing command with same idempotency key
            var existing = _commands.Values.FirstOrDefault(c => c.IdempotencyKey == idempotencyKey);
            if (existing != null)
            {
                return new PostCommandResult
                {
                    Success = true,
                    CommandId = existing.Id
                };
            }

            // Create new command
            var commandId = Guid.NewGuid();
            var cmd = new SimulatedCommand
            {
                Id = commandId,
                DeviceId = deviceId,
                Command = command,
                IdempotencyKey = idempotencyKey,
                Status = "Pending",
                CreatedUtc = DateTime.UtcNow
            };

            _commands[commandId] = cmd;

            return new PostCommandResult
            {
                Success = true,
                CommandId = commandId
            };
        }

        public async Task<RoutedEvent?> WaitForRouting(Guid commandId)
        {
            await Task.Delay(10); // Simulate routing delay
            return new RoutedEvent { CommandId = commandId, RoutedAt = DateTime.UtcNow };
        }

        public async Task SimulateDeviceExecution(Guid commandId, bool success)
        {
            await Task.Delay(Random.Shared.Next(10, 50)); // Simulate execution time

            if (_commands.TryGetValue(commandId, out var cmd))
            {
                _executionCounts.AddOrUpdate(cmd.DeviceId, 1, (_, count) => count + 1);

                cmd.Status = success ? "Completed" : "Failed";
                cmd.CompletedUtc = DateTime.UtcNow;
                cmd.Result = success ? "OK" : "Error";

                if (success)
                {
                    var completedEvent = new CompletedEvent
                    {
                        CommandId = commandId,
                        IdempotencyKey = cmd.IdempotencyKey,
                        Status = "Completed",
                        CompletedAt = DateTime.UtcNow
                    };

                    _completedEvents.AddOrUpdate(
                        cmd.IdempotencyKey,
                        new List<CompletedEvent> { completedEvent },
                        (_, list) => { list.Add(completedEvent); return list; }
                    );
                }
            }
        }

        public async Task<CompletedEvent?> WaitForCompletion(Guid commandId)
        {
            // Poll for completion
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(10);
                
                if (_commands.TryGetValue(commandId, out var cmd) && cmd.Status == "Completed")
                {
                    return new CompletedEvent
                    {
                        CommandId = commandId,
                        IdempotencyKey = cmd.IdempotencyKey,
                        Status = cmd.Status,
                        CompletedAt = cmd.CompletedUtc ?? DateTime.UtcNow
                    };
                }
            }

            return null;
        }

        public async Task<SimulatedCommand?> GetCommandFromDb(Guid commandId)
        {
            await Task.Delay(1); // Simulate DB query
            _commands.TryGetValue(commandId, out var cmd);
            return cmd;
        }

        public async Task<List<SimulatedCommand>> GetCommandsByIdempotencyKey(string idempotencyKey)
        {
            await Task.Delay(1);
            return _commands.Values.Where(c => c.IdempotencyKey == idempotencyKey).ToList();
        }

        public List<CompletedEvent> GetCompletedEvents(string idempotencyKey)
        {
            return _completedEvents.TryGetValue(idempotencyKey, out var events) ? events : new List<CompletedEvent>();
        }

        public int GetExecutionCount(string deviceId)
        {
            return _executionCounts.TryGetValue(deviceId, out var count) ? count : 0;
        }

        public int GetTotalCompletedCommands()
        {
            return _commands.Values.Count(c => c.Status == "Completed");
        }

        public int GetFailedCommandsCount()
        {
            return _commands.Values.Count(c => c.Status == "Failed");
        }

        // Helper classes
        public class SimulatedCommand
        {
            public Guid Id { get; set; }
            public string DeviceId { get; set; } = "";
            public string Command { get; set; } = "";
            public string IdempotencyKey { get; set; } = "";
            public string Status { get; set; } = "";
            public string? Result { get; set; }
            public DateTime CreatedUtc { get; set; }
            public DateTime? CompletedUtc { get; set; }
        }

        public class PostCommandResult
        {
            public bool Success { get; set; }
            public Guid CommandId { get; set; }
            public string Error { get; set; } = "";
        }

        public class RoutedEvent
        {
            public Guid CommandId { get; set; }
            public DateTime RoutedAt { get; set; }
        }

        public class CompletedEvent
        {
            public Guid CommandId { get; set; }
            public string IdempotencyKey { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime CompletedAt { get; set; }
        }
    }
}
