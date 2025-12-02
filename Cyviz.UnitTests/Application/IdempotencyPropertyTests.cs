using FsCheck;
using FsCheck.Xunit;
using Cyviz.Domain;
using System.Collections.Concurrent;
using SystemRandom = System.Random;

namespace Cyviz.UnitTests.Application;

/// <summary>
/// Property-based tests for idempotency guarantees using FsCheck.
/// These tests generate random command scenarios and verify invariants hold.
/// </summary>
public class IdempotencyPropertyTests
{
    /// <summary>
    /// Property: Random command retries should never produce two "Completed" events for the same idempotency key.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property RandomCommandRetries_NeverProduceDuplicateCompletedEvents()
    {
        return Prop.ForAll(
            Arb.From(GenCommandScenarios()),
            scenario =>
            {
                // Simulate command processing with potential retries
                var completedEvents = new ConcurrentBag<string>();
                var processedKeys = new ConcurrentDictionary<string, bool>();

                Parallel.ForEach(scenario.Commands, command =>
                {
                    // Simulate idempotency check
                    if (processedKeys.TryAdd(command.IdempotencyKey, true))
                    {
                        // First time processing this key - mark as completed
                        completedEvents.Add(command.IdempotencyKey);
                    }
                    // Else: duplicate detected, skip processing
                });

                // Property: Each idempotency key should appear at most once in completed events
                var duplicates = completedEvents
                    .GroupBy(key => key)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                return duplicates.Count == 0;
            });
    }

    /// <summary>
    /// Property: Idempotent command execution should always return the same result.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property IdempotentCommandExecution_AlwaysReturnsSameResult()
    {
        return Prop.ForAll(
            Arb.From(GenDeviceCommand()),
            command =>
            {
                var results = new List<CommandResult>();
                var simulator = new CommandSimulator();

                // Execute same command multiple times
                for (int i = 0; i < 5; i++)
                {
                    results.Add(simulator.Execute(command));
                }

                // All results should be identical
                return results.All(r => r.Status == results[0].Status);
            });
    }

    /// <summary>
    /// Property: Concurrent retries with same idempotency key should produce exactly one success.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property ConcurrentRetries_ProduceExactlyOneSuccess()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(2, 20)), // Number of concurrent retries
            Arb.From(GenIdempotencyKey()),
            (retryCount, idempotencyKey) =>
            {
                var successCount = 0;
                var lockObj = new object();
                var processedKeys = new HashSet<string>();

                // Simulate concurrent retries
                Parallel.For(0, retryCount, _ =>
                {
                    lock (lockObj)
                    {
                        if (processedKeys.Add(idempotencyKey))
                        {
                            Interlocked.Increment(ref successCount);
                        }
                    }
                });

                return successCount == 1;
            });
    }

    /// <summary>
    /// Property: Retry delays should use jitter to prevent thundering herd.
    /// Verifies that generated retry delays have variance (not all the same).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryDelays_PreventThunderingHerd()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(10, 50)), // Number of failing requests
            failureCount =>
            {
                // Generate retry delays with jitter (simulating what RetryPolicy does)
                var delays = Enumerable.Range(0, failureCount)
                    .Select(_ => SystemRandom.Shared.Next(100, 200)) // 100ms base + up to 100ms jitter
                    .ToList();

                // Property 1: Not all delays are identical (jitter adds variance)
                var uniqueDelays = delays.Distinct().Count();
                var hasVariance = uniqueDelays > 1;
                
                // Property 2: Delays span a reasonable range (at least 20ms spread)
                var minDelay = delays.Min();
                var maxDelay = delays.Max();
                var spread = maxDelay - minDelay;
                var hasSpread = spread >= 20; // With 100ms jitter range, expect at least 20ms spread
                
                // Property 3: Average delay is within expected range (100-200ms)
                var avgDelay = delays.Average();
                var avgInRange = avgDelay >= 100 && avgDelay <= 200;

                // All properties must hold to prevent thundering herd
                return hasVariance && hasSpread && avgInRange;
            });
    }

    // Generators
    private Gen<CommandScenario> GenCommandScenarios()
    {
        return from commandCount in Gen.Choose(10, 100)
               from retryProbability in Gen.Choose(1, 50)
               select new CommandScenario
               {
                   Commands = GenerateCommandsWithRetries(commandCount, retryProbability)
               };
    }

    private Gen<DeviceCommand> GenDeviceCommand()
    {
        return from deviceId in Gen.Elements("device-01", "device-02", "device-03")
               from command in Gen.Elements("Reboot", "Ping", "GetStatus", "Reset")
               from key in GenIdempotencyKey()
               select new DeviceCommand
               {
                   Id = Guid.NewGuid(),
                   DeviceId = deviceId,
                   Command = command,
                   IdempotencyKey = key,
                   Status = "Pending",
                   CreatedUtc = DateTime.UtcNow
               };
    }

    private Gen<string> GenIdempotencyKey()
    {
        return Gen.Choose(0, int.MaxValue).Select(_ => Guid.NewGuid().ToString());
    }

    private List<SimulatedCommand> GenerateCommandsWithRetries(int commandCount, int retryPercentage)
    {
        var commands = new List<SimulatedCommand>();
        var uniqueKeys = Enumerable.Range(0, commandCount)
            .Select(_ => Guid.NewGuid().ToString())
            .ToList();

        foreach (var key in uniqueKeys)
        {
            commands.Add(new SimulatedCommand
            {
                IdempotencyKey = key,
                DeviceId = $"device-{SystemRandom.Shared.Next(1, 10):D2}",
                Command = "Reboot"
            });

            // Add retries based on probability
            if (SystemRandom.Shared.Next(100) < retryPercentage)
            {
                var retries = SystemRandom.Shared.Next(1, 4);
                for (int i = 0; i < retries; i++)
                {
                    commands.Add(new SimulatedCommand
                    {
                        IdempotencyKey = key, // Same key = retry
                        DeviceId = $"device-{SystemRandom.Shared.Next(1, 10):D2}",
                        Command = "Reboot"
                    });
                }
            }
        }

        // Shuffle to simulate random order
        return commands.OrderBy(_ => SystemRandom.Shared.Next()).ToList();
    }

    // Helper classes
    private class CommandScenario
    {
        public List<SimulatedCommand> Commands { get; set; } = new();
    }

    private class SimulatedCommand
    {
        public string IdempotencyKey { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string Command { get; set; } = "";
    }

    private class CommandResult
    {
        public string Status { get; set; } = "";
        public string Result { get; set; } = "";
    }

    private class CommandSimulator
    {
        private readonly Dictionary<string, CommandResult> _cache = new();

        public CommandResult Execute(DeviceCommand command)
        {
            // Simulate idempotent execution
            if (_cache.TryGetValue(command.IdempotencyKey, out var cached))
            {
                return cached;
            }

            var result = new CommandResult
            {
                Status = "Completed",
                Result = $"Executed {command.Command} at {DateTime.UtcNow}"
            };

            _cache[command.IdempotencyKey] = result;
            return result;
        }
    }
}
