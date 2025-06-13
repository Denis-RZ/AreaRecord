using System.Threading;

namespace MyWebApp.Services;

public class QueryMetrics
{
    private long _count;
    private long _totalTicks;

    public void Add(TimeSpan duration)
    {
        Interlocked.Increment(ref _count);
        Interlocked.Add(ref _totalTicks, duration.Ticks);
    }

    public double AverageMilliseconds => _count == 0 ? 0 : (_totalTicks / (double)_count) / TimeSpan.TicksPerMillisecond;
}
