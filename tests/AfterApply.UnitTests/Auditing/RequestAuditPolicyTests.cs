using AfterApply.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Shouldly;

namespace AfterApply.UnitTests.Auditing;

public class RequestAuditPolicyTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("post")]
    public void A_Write_To_An_Api_Endpoint_Is_Audited(string method)
    {
        RequestAuditPolicy.ShouldAudit(Context(method, "/api/applications", endpoint: Endpoint())).ShouldBeTrue();
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void A_Read_Is_Not_Input(string method)
    {
        RequestAuditPolicy.ShouldAudit(Context(method, "/api/applications", endpoint: Endpoint())).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/hubs/import-progress/negotiate")]
    [InlineData("/apikeys")]
    public void Anything_Outside_Api_Is_Plumbing(string path)
    {
        RequestAuditPolicy.ShouldAudit(Context("POST", path, endpoint: Endpoint())).ShouldBeFalse();
    }

    [Fact]
    public void A_Request_That_Matched_No_Endpoint_Is_Noise()
    {
        RequestAuditPolicy.ShouldAudit(Context("POST", "/api/does-not-exist", endpoint: null)).ShouldBeFalse();
    }

    [Fact]
    public void An_Endpoint_That_Opted_Out_Is_Skipped()
    {
        var optedOut = Endpoint(SkipRequestAuditMetadata.Instance);

        RequestAuditPolicy.ShouldAudit(Context("POST", "/api/site-traffic/events", optedOut)).ShouldBeFalse();
    }

    private static HttpContext Context(string method, string path, Endpoint? endpoint)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Features.Set<IEndpointFeature>(new EndpointFeature { Endpoint = endpoint });
        return context;
    }

    private static Endpoint Endpoint(params object[] metadata) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");

    private sealed class EndpointFeature : IEndpointFeature
    {
        public Endpoint? Endpoint { get; set; }
    }
}
