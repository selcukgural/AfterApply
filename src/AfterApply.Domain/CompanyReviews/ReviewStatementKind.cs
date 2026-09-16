namespace AfterApply.Domain.CompanyReviews;

/// <summary>Whether a predefined statement says what the reviewer liked or what could improve.
/// The two lists are capped separately (<see cref="ReviewStatementCatalogue.MaxPicksPerKind"/>).</summary>
public enum ReviewStatementKind
{
    Liked,
    Improve
}
