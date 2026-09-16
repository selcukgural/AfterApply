namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// The eleven things a reviewer can rate. <see cref="Overall"/> is the one required rating and is
/// stored on the review row itself (<c>OverallRating</c>, which the Bayesian company score reads);
/// the other ten are optional and live in <c>CompanyReviewCategoryRatings</c>. Order here is the
/// order the form and the summary panel show them in.
/// </summary>
public enum ReviewCategory
{
    Overall,
    WorkEnvironment,
    Management,
    CareerGrowth,
    WorkLifeBalance,
    Pay,
    Benefits,
    RemoteWork,
    Tooling,
    Hiring,
    Onboarding
}
