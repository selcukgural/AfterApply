namespace AfterApply.Domain.Ai;

/// <summary>
/// One model call, as the bill will see it: which feature, which model, how many tokens each way.
/// This is the ledger the Pro cost model (DECISIONS.md 2026-09-12) asked for — the number the
/// "never at a loss" rule is checked against comes from here, not from a provider dashboard read
/// after the fact. Tokens are stored, not dollars: prices change, and the cost of a call written
/// at one price would be a wrong number a month later.
///
/// The user is optional and set to null when the account goes: the row is a cost record, not user
/// content, and losing it would make the month's total wrong. It carries nothing the user wrote —
/// no prompt, no output — so keeping it after the account is deleted keeps nothing of theirs.
/// </summary>
public sealed class AiUsageEntry
{
    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public AiFeature Feature { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public bool Succeeded { get; private set; }

    public DateTimeOffset At { get; private set; }

    public const int MaxModelLength = 100;

    private AiUsageEntry()
    {
    }

    public static AiUsageEntry Create(Guid? userId, AiFeature feature, string model, int inputTokens, int outputTokens, bool succeeded,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Feature = feature,
        Model = model.Length <= MaxModelLength ? model : model[..MaxModelLength],
        InputTokens = Math.Max(0, inputTokens),
        OutputTokens = Math.Max(0, outputTokens),
        Succeeded = succeeded,
        At = now
    };
}

public enum AiFeature
{
    /// <summary>The weekly job-fit score behind the paid job matching.</summary>
    JobFitScoring = 1
}
