using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

public enum BlogCommentReportReason
{
    Spam,
    Insult,
    Inappropriate,
    Advertising,
    Other
}

public enum BlogCommentReportStatus
{
    Open,
    Dismissed,
    ActionTaken
}

/// <summary>
/// A reader flagging an approved comment. One per reader per comment (unique index) — a second
/// report from the same reader is answered with the first. An admin closes it by rejecting the
/// comment (<see cref="BlogCommentReportStatus.ActionTaken"/>, every open report on the comment
/// at once) or by dismissing it; the comment itself changes only through
/// <see cref="BlogComment.Reject"/>. The <c>CompanyReviewReport</c> shape, without the written
/// resolution: a rejected comment's author gets no reason (MVP, 2026-09-20).
/// </summary>
public sealed class BlogCommentReport : Entity
{
    public const int MaxNoteLength = 500;

    public Guid CommentId { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public BlogCommentReportReason Reason { get; private set; }

    public string? Note { get; private set; }

    public BlogCommentReportStatus Status { get; private set; }

    public DateTimeOffset ReportedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Audit only, no foreign key.</summary>
    public Guid? ResolvedByUserId { get; private set; }

    private BlogCommentReport()
    {
    }

    /// <exception cref="BlogCommentReportNoteRequiredException">Reason "Other" without a note.</exception>
    public static BlogCommentReport Create(Guid commentId, Guid reporterUserId, BlogCommentReportReason reason, string? note,
        DateTimeOffset now)
    {
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (reason == BlogCommentReportReason.Other && trimmedNote is null)
        {
            throw new BlogCommentReportNoteRequiredException();
        }

        return new BlogCommentReport
        {
            CommentId = commentId,
            ReporterUserId = reporterUserId,
            Reason = reason,
            Note = trimmedNote,
            Status = BlogCommentReportStatus.Open,
            ReportedAt = now
        };
    }

    public void Resolve(BlogCommentReportStatus outcome, Guid adminUserId, DateTimeOffset now)
    {
        if (outcome == BlogCommentReportStatus.Open)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A resolution closes the report.");
        }

        if (Status != BlogCommentReportStatus.Open)
        {
            return;
        }

        Status = outcome;
        ResolvedAt = now;
        ResolvedByUserId = adminUserId;
    }
}
