using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

public class ApplicationStatusClassificationTests
{
    [Fact]
    public void RespondedStatuses_Contains_Expected_Members()
    {
        ApplicationStatusClassification.RespondedStatuses.ShouldBe(
        [
            ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
            ApplicationStatus.FinalInterview, ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Accepted
        ], ignoreOrder: true);
    }

    [Fact]
    public void InterviewStatuses_Contains_Expected_Members()
    {
        ApplicationStatusClassification.InterviewStatuses.ShouldBe(
        [
            ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview, ApplicationStatus.FinalInterview
        ], ignoreOrder: true);
    }

    [Fact]
    public void OfferStatuses_Contains_Expected_Members()
    {
        ApplicationStatusClassification.OfferStatuses.ShouldBe(
        [
            ApplicationStatus.Offer, ApplicationStatus.Accepted
        ], ignoreOrder: true);
    }
}
