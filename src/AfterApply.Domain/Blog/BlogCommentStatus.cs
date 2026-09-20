namespace AfterApply.Domain.Blog;

/// <summary>Where a reader's comment stands (2026-09-20). Every comment starts <see cref="Pending"/>
/// and reaches the page only as <see cref="Approved"/>; <see cref="Rejected"/> never does.</summary>
public enum BlogCommentStatus
{
    Pending,
    Approved,
    Rejected
}
