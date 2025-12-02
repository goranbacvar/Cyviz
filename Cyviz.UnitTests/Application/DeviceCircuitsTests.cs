using Cyviz.Application;

namespace Cyviz.UnitTests.Application;

public class DeviceCircuitsTests
{
    [Fact]
    public void DeviceCircuits_CreatesNewCircuitBreakerForDevice()
    {
        // Arrange
        var circuits = new DeviceCircuits();

        // Act
        var cb1 = circuits.Get("device-01");
        var cb2 = circuits.Get("device-01");

        // Assert
        cb1.Should().NotBeNull();
        cb2.Should().BeSameAs(cb1, "should return same instance for same device");
    }

    [Fact]
    public void DeviceCircuits_IsolatesCircuitsByDevice()
    {
        // Arrange
        var circuits = new DeviceCircuits();

        // Act
        var cb1 = circuits.Get("device-01");
        var cb2 = circuits.Get("device-02");

        // Record failures on device-01
        for (int i = 0; i < 5; i++)
        {
            cb1.RecordFailure();
        }

        // Assert
        cb1.IsOpen.Should().BeTrue("device-01 circuit should be open");
        cb2.IsOpen.Should().BeFalse("device-02 circuit should be independent");
    }

    [Fact]
    public void DeviceCircuits_HandlesMultipleDevicesConcurrently()
    {
        // Arrange
        var circuits = new DeviceCircuits();
        var deviceIds = Enumerable.Range(1, 10).Select(i => $"device-{i:D2}").ToList();

        // Act - Access circuits concurrently
        var tasks = deviceIds.Select(deviceId => Task.Run(() =>
        {
            var cb = circuits.Get(deviceId);
            cb.RecordFailure();
            return cb;
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        var circuitBreakerInstances = tasks.Select(t => t.Result).ToList();
        circuitBreakerInstances.Should().HaveCount(10);
        circuitBreakerInstances.Should().OnlyHaveUniqueItems("each device should have unique circuit");
    }

    [Fact]
    public void DeviceCircuits_ThreadSafe()
    {
        // Arrange
        var circuits = new DeviceCircuits();
        var exceptions = new List<Exception>();

        // Act - Stress test with concurrent access
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            try
            {
                var deviceId = $"device-{Random.Shared.Next(1, 20):D2}";
                var cb = circuits.Get(deviceId);
                
                if (Random.Shared.Next(2) == 0)
                {
                    cb.RecordFailure();
                }
                else
                {
                    cb.RecordSuccess();
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("should not throw exceptions under concurrent access");
    }

    [Fact]
    public void DeviceCircuits_ReturnsConsistentInstance()
    {
        // Arrange
        var circuits = new DeviceCircuits();
        var deviceId = "device-test";

        // Act - Get circuit multiple times
        var instances = Enumerable.Range(0, 10)
            .Select(_ => circuits.Get(deviceId))
            .ToList();

        // Assert
        instances.Should().HaveCount(10);
        instances.Should().OnlyContain(cb => cb == instances[0], 
            "all calls should return the same instance");
    }
}
