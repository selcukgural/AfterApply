using AfterApply.Application.Common;

namespace AfterApply.Application.Applications;

/// <summary>
/// The selection resolves to more applications than one request is allowed to load and mutate.
/// Carries the limit so the message can quote the number this deployment actually runs with rather
/// than a constant baked into a translation file.
/// </summary>
public sealed class BulkOperationTooLargeException(int maxOperationSize)
    : CodedException("BULK_OPERATION_TOO_LARGE",
        $"A bulk operation may cover at most {maxOperationSize} applications.", maxOperationSize)
{
    public int MaxOperationSize { get; } = maxOperationSize;
}

/// <summary>
/// The number of applications matching the operation's filter is not the number the user was shown.
/// Thrown before anything is written, so nothing has changed when a caller sees this — the point is
/// that a row created (or a status changed) between the screen being drawn and the button being
/// pressed must not be swept into a permanent delete the user never agreed to.
/// </summary>
public sealed class BulkCountMismatchException(int expectedCount, int actualCount)
    : CodedException("BULK_COUNT_MISMATCH",
        $"The selection matched {actualCount} applications, not the {expectedCount} the caller expected.")
{
    public int ExpectedCount { get; } = expectedCount;

    public int ActualCount { get; } = actualCount;
}
