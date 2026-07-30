namespace ECSPros.Api.Hubs;

/// <summary>
/// DashboardHub'a bağlı istemci sayısını tutar. DashboardMetricsWorker,
/// kimse bağlı değilken metrik sorgularını atlamak için bunu okur
/// (docs/SiteYavaslikDegerlendirme.txt — boşa çalışan periyodik sorgular).
/// </summary>
public class DashboardPresenceTracker
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);

    // Bağlantı kopuşları çakışırsa sayaç negatife düşmesin
    public void Decrement()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _count);
            if (current == 0) return;
        } while (Interlocked.CompareExchange(ref _count, current - 1, current) != current);
    }
}
