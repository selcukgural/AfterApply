using System.Net;
using AfterApply.Api.RateLimits;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace AfterApply.UnitTests.RateLimits;

/// <summary>Who an anonymous request counts against (2026-09-24): an IPv6 caller as its /64, and a
/// server-side render as the visitor it names — only when it holds the render key.</summary>
public class ClientPartitionTests
{
    private const string Key = "render-secret";

    [Fact]
    public void Every_Address_In_One_IPv6_Slash_64_Is_One_Caller()
    {
        var a = ClientPartition.ForAddress(IPAddress.Parse("2001:db8:abcd:12:1::1"));
        var b = ClientPartition.ForAddress(IPAddress.Parse("2001:db8:abcd:12:ffff:ffff:ffff:ffff"));
        var elsewhere = ClientPartition.ForAddress(IPAddress.Parse("2001:db8:abcd:13::1"));

        a.ShouldBe(b);
        a.ShouldBe("2001:db8:abcd:12::/64");
        elsewhere.ShouldNotBe(a);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    public void An_IPv4_Caller_Is_Its_Address(string address, string key) =>
        ClientPartition.ForAddress(IPAddress.Parse(address)).ShouldBe(key);

    [Fact]
    public void No_Address_Is_One_Shared_Partition() =>
        ClientPartition.ForAddress(null).ShouldBe("unknown");

    [Fact]
    public void A_Render_With_The_Key_Counts_As_The_Visitor_It_Names() =>
        ClientPartition.ClientAddress(Render(Key, "198.51.100.4"), Key).ShouldBe(IPAddress.Parse("198.51.100.4"));

    [Theory]
    [InlineData("wrong-secret", "198.51.100.4")]
    [InlineData(null, "198.51.100.4")]
    [InlineData(Key, "not-an-address")]
    public void Anything_Else_Counts_As_The_Connection(string? presentedKey, string visitor) =>
        ClientPartition.ClientAddress(Render(presentedKey, visitor), Key).ShouldBe(IPAddress.Parse("10.0.0.9"));

    [Fact]
    public void Without_A_Configured_Key_Nobody_Can_Name_A_Visitor() =>
        ClientPartition.ClientAddress(Render("", "198.51.100.4"), serverRenderKey: null).ShouldBe(IPAddress.Parse("10.0.0.9"));

    private static DefaultHttpContext Render(string? key, string visitor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.9");
        if (key is not null)
        {
            context.Request.Headers[ClientPartition.KeyHeader] = key;
        }

        context.Request.Headers[ClientPartition.ClientHeader] = visitor;
        return context;
    }
}
