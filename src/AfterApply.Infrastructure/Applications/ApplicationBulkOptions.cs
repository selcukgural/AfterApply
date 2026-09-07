namespace AfterApply.Infrastructure.Applications;

public sealed class ApplicationBulkOptions
{
    public const string SectionName = "ApplicationBulkOperations";

    /// <summary>
    /// The most applications one bulk operation may load and mutate row by row. It bounds the two
    /// paths that have to materialise entities — a status change (every row gets a history row and
    /// a timeline event) and an undo — so a single request cannot turn into an unbounded write.
    ///
    /// Deliberately NOT applied to a delete resolved from a filter: that one is a set-based
    /// ExecuteDelete with no entities loaded, and capping it would break the one feature the ceiling
    /// would be protecting — "Delete All" on an account with more rows than this.
    /// </summary>
    public int MaxOperationSize { get; init; } = 500;
}
