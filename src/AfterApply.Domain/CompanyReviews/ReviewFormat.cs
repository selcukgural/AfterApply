namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// Which shape a review row holds. <see cref="Legacy"/> rows were written before 2026-09-16 and
/// carry a title, pros and cons in free text plus four fixed category ratings; they are kept
/// exactly as written but their text is no longer shown to the public. <see cref="Structured"/>
/// rows carry only ratings and catalogue statement keys. The column defaults to Legacy in the
/// database so a row inserted by an older instance during a rollout is labelled correctly.
/// </summary>
public enum ReviewFormat
{
    Legacy,
    Structured
}
