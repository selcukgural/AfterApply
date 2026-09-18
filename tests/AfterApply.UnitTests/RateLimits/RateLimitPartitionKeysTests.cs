using AfterApply.Api.RateLimits;
using Shouldly;

namespace AfterApply.UnitTests.RateLimits;

public class RateLimitPartitionKeysTests
{
    [Fact]
    public void The_Redis_Key_Carries_The_Policy_But_Never_The_Caller()
    {
        var key = RateLimitPartitionKeys.ForRedis("ek:", "cv-scan", "203.0.113.7");

        key.ShouldStartWith("ek:cv-scan:");
        key.ShouldNotContain("203.0.113.7");
        key.Length.ShouldBe("ek:cv-scan:".Length + 24);
    }

    [Fact]
    public void The_Same_Caller_Under_Two_Policies_Gets_Two_Windows()
    {
        // Seven policies partition by IP; the library keys a window on the partition alone, so
        // without the policy in the key one address would share one window across all of them.
        RateLimitPartitionKeys.ForRedis("ek:", "auth-strict", "203.0.113.7")
            .ShouldNotBe(RateLimitPartitionKeys.ForRedis("ek:", "benchmark", "203.0.113.7"));
    }

    [Fact]
    public void The_Same_Caller_Gets_The_Same_Window_Every_Time()
    {
        RateLimitPartitionKeys.ForRedis("ek:", "auth-strict", "203.0.113.7")
            .ShouldBe(RateLimitPartitionKeys.ForRedis("ek:", "auth-strict", "203.0.113.7"));
        RateLimitPartitionKeys.ForRedis("ek:", "auth-strict", "203.0.113.7")
            .ShouldNotBe(RateLimitPartitionKeys.ForRedis("ek:", "auth-strict", "203.0.113.8"));
    }
}
