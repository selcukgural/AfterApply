using System.Net;
using AfterApply.Infrastructure.Http;
using Shouldly;

namespace AfterApply.UnitTests.Infrastructure;

public class PublicAddressGuardTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("185.60.216.35")]
    [InlineData("100.63.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void Addresses_On_The_Public_Internet_Are_Allowed(string address) =>
        PublicAddressGuard.IsPublic(IPAddress.Parse(address)).ShouldBeTrue();

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")] // the cloud metadata server
    [InlineData("100.64.0.1")]      // carrier-grade NAT
    [InlineData("0.0.0.0")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:127.0.0.1")]  // IPv4-mapped loopback
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a9fe:a9fe")] // NAT64 of 169.254.169.254
    [InlineData("2002:0a00:0001::1")]  // 6to4 of 10.0.0.1
    [InlineData("::7f00:1")]           // IPv4-compatible 127.0.0.1
    public void Loopback_Private_Link_Local_And_Reserved_Addresses_Are_Refused(string address) =>
        PublicAddressGuard.IsPublic(IPAddress.Parse(address)).ShouldBeFalse();

    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("http://localhost:9/")]
    [InlineData("http://[::1]:9/")]
    public async Task A_Request_Never_Connects_To_A_Name_Or_Literal_That_Is_Not_Public(string url)
    {
        // Resolution is local (a literal, or localhost), so this opens no socket anywhere.
        using var client = new HttpClient(PublicAddressGuard.CreateHandler());

        var failure = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync(url));

        PublicAddressGuard.IsRefusal(failure).ShouldBeTrue();
    }

    [Fact]
    public void A_Network_Failure_Is_Not_Mistaken_For_A_Refusal() =>
        PublicAddressGuard.IsRefusal(new HttpRequestException("connection reset", new IOException())).ShouldBeFalse();

    [Fact]
    public void The_Handler_Follows_No_Redirect_And_Keeps_No_Cookies()
    {
        using var handler = PublicAddressGuard.CreateHandler();

        handler.AllowAutoRedirect.ShouldBeFalse();
        handler.UseCookies.ShouldBeFalse();
        handler.UseProxy.ShouldBeFalse();
    }
}
