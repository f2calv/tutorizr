namespace CasCap.TestData;

/// <summary><see cref="TimeProvider"/> whose clock only moves when a test advances it.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset startUtc) : TimeProvider
{
    private DateTimeOffset _now = startUtc;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}
