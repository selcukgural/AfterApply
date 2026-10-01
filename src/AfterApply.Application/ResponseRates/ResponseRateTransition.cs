using AfterApply.Domain.Applications;

namespace AfterApply.Application.ResponseRates;

/// <summary>One status-history row as the cross-user aggregates read it: the move, when, and how it
/// was applied. The origin is what lets <see cref="ResponseRateAggregator.ToSample(Guid, Guid, ApplicationStatus, DateTimeOffset, IEnumerable{ResponseRateTransition}, DateOnly?, DateTimeOffset?, RejectionNotice?, DateTimeOffset)"/>
/// drop a change the user undid and keep an import's date out of the reply-time figures.</summary>
public readonly record struct ResponseRateTransition(
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    DateTimeOffset ChangedAt,
    StatusChangeOrigin Origin);
