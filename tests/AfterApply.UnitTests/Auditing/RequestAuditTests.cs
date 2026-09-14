using AfterApply.Domain.Auditing;
using Shouldly;

namespace AfterApply.UnitTests.Auditing;

public class RequestAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Keeps_What_Was_Given()
    {
        var userId = Guid.CreateVersion7();

        var audit = RequestAudit.Create(userId, "post", "/api/companies/abc/reviews", 201, "203.0.113.7", Now);

        audit.UserId.ShouldBe(userId);
        audit.Method.ShouldBe("POST");
        audit.Path.ShouldBe("/api/companies/abc/reviews");
        audit.StatusCode.ShouldBe(201);
        audit.IpAddress.ShouldBe("203.0.113.7");
        audit.At.ShouldBe(Now);
    }

    [Fact]
    public void Anonymous_Request_Has_No_User()
    {
        var audit = RequestAudit.Create(null, "POST", "/api/cv-scan", 200, "2001:db8::1", Now);

        audit.UserId.ShouldBeNull();
        audit.IpAddress.ShouldBe("2001:db8::1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Missing_Ip_Is_Stored_As_Null_Not_Empty(string? ipAddress)
    {
        var audit = RequestAudit.Create(null, "POST", "/api/x", 200, ipAddress, Now);

        audit.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void Overlong_Values_Are_Cut_To_The_Column_Width()
    {
        var longPath = "/api/" + new string('a', 600);
        var longIp = new string('f', 60);

        var audit = RequestAudit.Create(null, "POST", longPath, 200, longIp, Now);

        audit.Path.Length.ShouldBe(RequestAudit.MaxPathLength);
        audit.IpAddress!.Length.ShouldBe(RequestAudit.MaxIpAddressLength);
    }

    [Fact]
    public void A_Blank_Path_Becomes_Root()
    {
        RequestAudit.Create(null, "POST", "", 200, null, Now).Path.ShouldBe("/");
    }

    [Fact]
    public void A_Blank_Method_Is_Rejected()
    {
        Should.Throw<ArgumentException>(() => RequestAudit.Create(null, " ", "/api/x", 200, null, Now));
    }
}
