using AfterApply.Application.Feedback.Contracts;

namespace AfterApply.Application.Feedback;

public interface IFeedbackService
{
    /// <param name="userAgent">The caller's <c>User-Agent</c> header, or null when it sent none.</param>
    Task<FeedbackResponse> SubmitAsync(Guid userId, SubmitFeedbackRequest request, string? userAgent,
        CancellationToken cancellationToken);
}
