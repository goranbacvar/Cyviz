using Cyviz.Application;

namespace Cyviz.UnitTests.Application;

public class CircuitBreakerTests
{
    [Fact]
    public void CircuitBreaker_StartsInClosedState()
    {
        // Arrange
        var cb = new CircuitBreaker();

        // Assert
        cb.IsOpen.Should().BeFalse();
        cb.IsHalfOpen.Should().BeFalse();
    }

    [Fact]
    public void CircuitBreaker_OpensAfter5Failures()
    {
        // Arrange
        var cb = new CircuitBreaker();

        // Act
        for (int i = 0; i < 4; i++)
        {
            cb.RecordFailure();
            cb.IsOpen.Should().BeFalse($"should not open before 5 failures (at {i + 1})");
        }

        cb.RecordFailure(); // 5th failure

        // Assert
        cb.IsOpen.Should().BeTrue("circuit should open after 5 failures");
    }

    [Fact]
    public async Task CircuitBreaker_MovesToHalfOpenAfter10Seconds()
    {
        // Arrange
        var cb = new CircuitBreaker();

        // Act - Record 5 failures to open the circuit
        for (int i = 0; i < 5; i++)
        {
            cb.RecordFailure();
        }

        cb.IsOpen.Should().BeTrue();
        cb.IsHalfOpen.Should().BeFalse();

        // Wait for half-open period
        await Task.Delay(TimeSpan.FromSeconds(10.1));

        // Assert
        cb.IsOpen.Should().BeFalse("circuit should no longer be open");
        cb.IsHalfOpen.Should().BeTrue("circuit should be in half-open state");
    }

    [Fact]
    public void CircuitBreaker_ResetsAfterSuccess()
    {
        // Arrange
        var cb = new CircuitBreaker();

        // Act - Record failures
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();

        // Record success
        cb.RecordSuccess();

        // Record more failures
        cb.RecordFailure();
        cb.RecordFailure();

        // Assert - Should not be open (reset after success)
        cb.IsOpen.Should().BeFalse("circuit should reset after success");
    }

    [Fact]
    public void CircuitBreaker_FailureCountResets()
    {
        // Arrange
        var cb = new CircuitBreaker();

        // Act
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess(); // Reset

        // Now need 5 more failures to open
        for (int i = 0; i < 4; i++)
        {
            cb.RecordFailure();
        }

        // Assert
        cb.IsOpen.Should().BeFalse("should need 5 consecutive failures after reset");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CircuitBreaker_DoesNotOpenBeforeFiveFailures(int failureCount)
    {
        // Arrange
        var cb = new CircuitBreaker();

        // Act
        for (int i = 0; i < failureCount; i++)
        {
            cb.RecordFailure();
        }

        // Assert
        cb.IsOpen.Should().BeFalse();
    }
}
