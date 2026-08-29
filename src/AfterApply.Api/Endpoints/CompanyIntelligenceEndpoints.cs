using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Infrastructure.CompanyIntelligence;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class CompanyIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapCompanyIntelligenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/company-intelligence").WithTags("CompanyIntelligence").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{companyId:guid}", async (Guid companyId, ICompanyIntelligenceService service,
            IOptions<CompanyIntelligenceOptions> options, CancellationToken cancellationToken) =>
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
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
            .WithSummary("Get aggregated response-rate intelligence for a company")
            .WithDescription("404 both when the CompanyIntelligence:Enabled flag is off and when the company has no " +
                             "data yet — the two cases are deliberately indistinguishable to the caller.")
            .Produces<CompanyIntelligenceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
