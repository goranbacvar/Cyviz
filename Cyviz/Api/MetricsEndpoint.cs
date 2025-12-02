using System.Collections.Concurrent;

namespace Cyviz.Api;

public static class MetricsEndpoint
{
    private static readonly ConcurrentDictionary<string, double> _metrics = new();
    public static IResult Handle()
    {
        var snapshot = _metrics.ToDictionary(k => k.Key, v => v.Value);
        return Results.Ok(snapshot);
    }
    public static void Set(string name, double value) => _metrics[name] = value;
}
