using AfterApply.Infrastructure;
using Hangfire;
using Shouldly;

namespace AfterApply.UnitTests.Infrastructure;

public class HangfireJobRetentionTests
{
    [Fact]
    public void A_Job_Out_Of_Retries_Is_Deleted_So_It_Expires_Instead_Of_Staying_Failed_Forever()
    {
        HangfireJobRetention.Apply();
        HangfireJobRetention.Apply();

        var retry = GlobalJobFilters.Filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().ShouldHaveSingleItem();
        retry.OnAttemptsExceeded.ShouldBe(AttemptsExceededAction.Delete);
        retry.Attempts.ShouldBe(HangfireJobRetention.RetryAttempts);
    }
}
