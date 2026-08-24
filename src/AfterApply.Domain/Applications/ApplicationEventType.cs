namespace AfterApply.Domain.Applications;

public enum ApplicationEventType
{
    ApplicationCreated,
    ApplicationSubmitted,
    RecruiterContacted,
    ScreeningStarted,
    InterviewScheduled,
    InterviewCompleted,
    OfferReceived,
    FollowUpSent,
    StatusChanged
}
