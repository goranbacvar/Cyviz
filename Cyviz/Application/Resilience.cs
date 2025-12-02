using System.Collections.Concurrent;

namespace Cyviz.Application;

public class CircuitBreaker
{
    private int _failures;
    private DateTime _openedAt;
    public bool IsOpen => _failures >= 5 && (DateTime.UtcNow - _openedAt) < TimeSpan.FromSeconds(10);
    public bool IsHalfOpen => _failures >= 5 && (DateTime.UtcNow - _openedAt) >= TimeSpan.FromSeconds(10);
    public void RecordSuccess() { _failures = 0; }
    public void RecordFailure() { _failures++; if (_failures == 5) _openedAt = DateTime.UtcNow; }
}

public static class RetryPolicy
{
    public static async Task<bool> ExecuteAsync(Func<Task<bool>> action, CancellationToken ct)
    {
        var rnd = new Random();
        int[] delays = { 100, 300, 700 };
        for (int i = 0; i < delays.Length; i++)
        {
            if (await action()) return true;
            var jitter = rnd.Next(0, 50);
            await Task.Delay(delays[i] + jitter, ct);
        }
        return false;
    }
}

public class DeviceCircuits
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> _map = new();
    public CircuitBreaker Get(string deviceId) => _map.GetOrAdd(deviceId, _ => new CircuitBreaker());
}
