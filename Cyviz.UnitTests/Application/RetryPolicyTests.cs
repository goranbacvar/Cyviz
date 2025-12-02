using Cyviz.Application;

namespace Cyviz.UnitTests.Application;

public class RetryPolicyTests
{
    [Fact]
    public async Task RetryPolicy_SucceedsOnFirstAttempt()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<bool>> action = () =>
        {
            attemptCount++;
            return Task.FromResult(true);
        };

        // Act
        var result = await RetryPolicy.ExecuteAsync(action, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        attemptCount.Should().Be(1, "should succeed on first attempt");
    }

    [Fact]
    public async Task RetryPolicy_RetriesOnFailure()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<bool>> action = () =>
        {
            attemptCount++;
            return Task.FromResult(attemptCount == 3); // Succeed on 3rd attempt
        };

        // Act
        var result = await RetryPolicy.ExecuteAsync(action, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        attemptCount.Should().Be(3, "should retry until success");
    }

    [Fact]
    public async Task RetryPolicy_FailsAfterThreeAttempts()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<bool>> action = () =>
        {
            attemptCount++;
            return Task.FromResult(false); // Always fail
        };

        // Act
        var result = await RetryPolicy.ExecuteAsync(action, CancellationToken.None);

        // Assert
        result.Should().BeFalse("should fail after all retries exhausted");
        attemptCount.Should().Be(3, "should attempt exactly 3 times");
    }

    [Fact]
    public async Task RetryPolicy_RespectsExponentialBackoff()
    {
        // Arrange
        var attemptTimes = new List<DateTime>();
        Func<Task<bool>> action = () =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            return Task.FromResult(false);
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await RetryPolicy.ExecuteAsync(action, CancellationToken.None);
        sw.Stop();

        // Assert
        attemptTimes.Should().HaveCount(3);
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(300, 
            "total delay should be at least 100ms + 300ms = 400ms minus jitter");
        
        // Verify delays between attempts
        if (attemptTimes.Count >= 2)
        {
            var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
            delay1.Should().BeInRange(100, 200, "first delay should be ~100ms + jitter");
        }
    }

    [Fact]
    public async Task RetryPolicy_HandlesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var attemptCount = 0;
        
        Func<Task<bool>> action = async () =>
        {
            attemptCount++;
            if (attemptCount == 2)
            {
                cts.Cancel();
                await Task.Delay(500, cts.Token); // This should throw
            }
            return false;
        };

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await RetryPolicy.ExecuteAsync(action, cts.Token);
        });

        attemptCount.Should().Be(2, "should stop on cancellation");
    }

    [Fact]
    public async Task RetryPolicy_AppliesJitter()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var attemptTimes = new List<DateTime>();
        
        Func<Task<bool>> action = () =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            return Task.FromResult(false);
        };

        // Act - Run multiple times to verify jitter variation
        for (int i = 0; i < 5; i++)
        {
            attemptTimes.Clear();
            await RetryPolicy.ExecuteAsync(action, CancellationToken.None);
            
            if (attemptTimes.Count >= 2)
            {
                delays.Add(attemptTimes[1] - attemptTimes[0]);
            }
        }

        // Assert - Delays should vary due to jitter (0-50ms random)
        delays.Should().HaveCountGreaterOrEqualTo(3);
        var uniqueDelays = delays.Select(d => (int)d.TotalMilliseconds).Distinct().Count();
        uniqueDelays.Should().BeGreaterThan(1, "jitter should cause variation in delays");
    }
}
