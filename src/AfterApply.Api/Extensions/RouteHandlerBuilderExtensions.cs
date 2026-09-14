using AfterApply.Api.Filters;
using AfterApply.Api.Middleware;

namespace AfterApply.Api.Extensions;

public static class RouteHandlerBuilderExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
    }

    /// <summary>
    /// Keeps RequestAuditMiddleware from recording this endpoint. Every write endpoint under
    /// /api is audited by default (CLAUDE.md "Request audit"); opting out is a privacy statement
    /// and needs a comment at the call site, a DECISIONS.md line and the allowlist in
    /// RequestAuditTests updated in the same change.
    /// </summary>
    public static TBuilder WithoutRequestAudit<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(SkipRequestAuditMetadata.Instance);
}
