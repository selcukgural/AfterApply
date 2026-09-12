namespace AfterApply.Domain.JobSources;

/// <summary>How far back a query looks. Maps to LinkedIn's <c>f_TPR=r{seconds}</c> filter.</summary>
public enum JobSourceTimeWindow
{
    Day = 1,
    Week = 7,
    Month = 30
}
