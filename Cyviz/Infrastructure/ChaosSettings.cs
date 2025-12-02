namespace Cyviz.Infrastructure;

public static class ChaosSettings
{
    public static double LatencyMinMs { get; private set; }
    public static double LatencyMaxMs { get; private set; }
    public static double DropRate { get; private set; }

    public static void Load(IConfiguration config)
    {
        var latency = Environment.GetEnvironmentVariable("CHAOS_LATENCY");
        if (!string.IsNullOrEmpty(latency) && latency.Contains('-'))
        {
            var parts = latency.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (double.TryParse(parts[0].Replace("s",""), out var min) && double.TryParse(parts[1].Replace("s",""), out var max))
            {
                LatencyMinMs = min * 1000; LatencyMaxMs = max * 1000;
            }
        }
        var drop = Environment.GetEnvironmentVariable("CHAOS_DROP_RATE");
        if (double.TryParse(drop, out var dr)) DropRate = dr;
    }
}
