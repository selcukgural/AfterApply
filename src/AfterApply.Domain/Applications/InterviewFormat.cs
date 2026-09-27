namespace AfterApply.Domain.Applications;

/// <summary>How an interview takes place. Only the kind — the meeting link itself is never stored:
/// it is a credential to someone else's call, and the candidate already has it in the invite.</summary>
public enum InterviewFormat
{
    Online,
    InPerson,
    Phone
}
