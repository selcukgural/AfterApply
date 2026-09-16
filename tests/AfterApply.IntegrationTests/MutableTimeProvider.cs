namespace AfterApply.IntegrationTests;

/// <summary>A clock a test moves by hand, so "next week" or "after the order expired" is a call
/// rather than a wait. One per class; <see cref="Set" /> back to the start between tests.</summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public DateTimeOffset Start { get; } = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset now) => _now = now;

    public void Reset() => _now = Start;
}
