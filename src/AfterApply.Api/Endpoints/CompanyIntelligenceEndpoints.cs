using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Infrastructure.CompanyIntelligence;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Api.Endpoints;

public static class CompanyIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapCompanyIntelligenceEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous since 2026-09-22: the company page reads it the way it reads reviews, without
        // an account. Nothing in the answer is about the caller, and below the threshold nothing
        // in it is about anyone — the thresholds, not sign-in, are what guard this data.
        var group = app.MapGroup("/api/company-intelligence").WithTags("CompanyIntelligence");

        group.MapGet("/{companyId:guid}", async (Guid companyId, ICompanyIntelligenceService service,
            IOptions<CompanyIntelligenceOptions> options, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            // Flag off → 404 for every caller, before the service is even called. Same status
            // code as "company not found" below, so the endpoint's mere existence isn't
            // distinguishable while the flag is off — DoD: "flag kapalıyken hiçbir uç noktadan
            // company-level veri sızmaz".
            if (!options.Value.Enabled)
            {
                return Results.NotFound();
            }

            var result = await service.GetByCompanyIdAsync(companyId, cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }

            // Public and short-lived: the figures move as people update statuses, but not by the
            // minute, and the same body goes to every caller.
            httpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromMinutes(10)
            };
            httpContext.Response.Headers.Vary = HeaderNames.Origin;
            return Results.Ok(result);
        })
            .WithSummary("Get aggregated response-rate intelligence for a company")
            .WithDescription("404 both when the CompanyIntelligence:Enabled flag is off and when the company has no " +
                             "data yet — the two cases are deliberately indistinguishable to the caller.")
            .Produces<CompanyIntelligenceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
