using AfterApply.Application.FeatureFlags;
using AfterApply.Application.Blog;
using AfterApply.Application.ClientConfig;
using AfterApply.Infrastructure.CompanyReviews;
using AfterApply.Infrastructure.CandidateExperiences;
using AfterApply.Infrastructure.CompanySalaries;
using AfterApply.Infrastructure.CvScan;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Api.Endpoints;

public static class ClientConfigEndpoints
{
    public static IEndpointRouteBuilder MapClientConfigEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous on purpose: the register and reset-password forms need the password rules
        // before there is an account. Not under the Auth rate-limit policy either — that bucket is
        // 5/min per IP and the form fetching its own rules would spend one of the five attempts a
        // user gets at actually registering. The global per-IP limiter still applies.
        app.MapGet("/api/config", async (
                IOptions<IdentityOptions> identityOptions,
                IOptions<PersonalAccessTokenOptions> personalAccessTokenOptions,
                IOptions<GoogleAuthOptions> googleAuthOptions,
                IOptions<LinkedInAuthOptions> linkedInAuthOptions,
                IOptions<GitHubAuthOptions> gitHubAuthOptions,
                IOptions<CvScanOptions> cvScanOptions,
                IOptions<CompanyReviewOptions> companyReviewOptions,
                IOptions<PayTrOptions> payTrOptions,
                IOptions<CompanySalaryOptions> companySalaryOptions,
                IOptions<CandidateExperienceOptions> candidateExperienceOptions,
                IFeatureFlags featureFlags,
                IBlogPublicService blog,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                // Read from IdentityOptions rather than IdentityPolicyOptions: the former is the object
                // PasswordValidator enforces, so whatever ends up here is by construction what a
                // submitted password will be checked against.
                var password = identityOptions.Value.Password;
                var tokens = personalAccessTokenOptions.Value;
                var google = googleAuthOptions.Value;
                var linkedIn = linkedInAuthOptions.Value;
                var gitHub = gitHubAuthOptions.Value;
                var cvScan = cvScanOptions.Value;
                var reviews = companyReviewOptions.Value;
                var salaries = companySalaryOptions.Value;
                var experiences = candidateExperienceOptions.Value;
                // The one value here that comes from the database rather than from options. It is
                // cached in the service and answers false on any failure, so this route keeps its
                // "never down" property (the sign-in buttons read it).
                var blogEnabled = featureFlags.IsEnabled(FeatureFlag.Blog);
                var hasPublishedPosts = blogEnabled && await blog.HasPublishedPostsAsync(cancellationToken);

                // The values change with a deploy or when an admin switches a flag (the flags are
                // runtime since 2026-09-27), so let browsers and the CDN hold them for a few minutes
                // instead of re-fetching on every form mount.
                httpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromMinutes(5)
                };
                // A public, cacheable response that is also CORS-served must vary on Origin: without
                // this, a copy fetched with no Origin (typing the URL into the address bar to check
                // it — a routine developer move) is stored without Access-Control-Allow-Origin, and
                // for the next five minutes the web app's cross-origin fetch is answered from that
                // copy and fails CORS. Seen live on 2026-09-05: the Google button silently vanished.
                httpContext.Response.Headers.Vary = HeaderNames.Origin;

                return Results.Ok(new ClientConfigResponse(
                    new PasswordPolicyResponse(
                        password.RequiredLength,
                        password.RequiredUniqueChars,
                        password.RequireDigit,
                        password.RequireLowercase,
                        password.RequireUppercase,
                        password.RequireNonAlphanumeric),
                    new PersonalAccessTokenLimitsResponse(tokens.MaxActiveTokens, tokens.LifetimeDays),
                    new GoogleAuthConfigResponse(google.IsConfigured, google.IsConfigured ? google.ClientId : null),
                    new LinkedInAuthConfigResponse(linkedIn.IsConfigured, linkedIn.IsConfigured ? linkedIn.ClientId : null),
                    new GitHubAuthConfigResponse(gitHub.IsConfigured, gitHub.IsConfigured ? gitHub.ClientId : null),
                    // Both halves have to be true for the box to be worth offering: the feature
                    // flag, and a project to call. A flag on with no project configured would
                    // render a checkbox whose only outcome is "unavailable".
                    new CvScanConfigResponse(featureFlags.IsEnabled(FeatureFlag.CvScan),
                        featureFlags.IsEnabled(FeatureFlag.CvScan) && featureFlags.IsEnabled(FeatureFlag.CvScanNotes)
                                             && !string.IsNullOrWhiteSpace(cvScan.Review.ProjectId)),
                    new CompanyReviewsConfigResponse(featureFlags.IsEnabled(FeatureFlag.CompanyReviews), reviews.MaxReviewsPerUser,
                        reviews.MinimumReviewsForScore, reviews.PriorWeight),
                    new CompanySalariesConfigResponse(featureFlags.IsEnabled(FeatureFlag.CompanySalaries), salaries.MaxEntriesPerUser,
                        salaries.MinimumEntriesForStats),
                    new JobSourcesConfigResponse(featureFlags.IsEnabled(FeatureFlag.JobSources)),
                    new PaymentsConfigResponse(featureFlags.IsEnabled(FeatureFlag.Payments) && payTrOptions.Value.IsConfigured),
                    new CandidateExperiencesConfigResponse(featureFlags.IsEnabled(FeatureFlag.CandidateExperiences), experiences.MaxEntriesPerUser,
                        experiences.MinimumEntriesForStats, experiences.PriorWeight),
                    new BlogConfigResponse(blogEnabled, hasPublishedPosts),
                    new CompanyIntelligenceConfigResponse(featureFlags.IsEnabled(FeatureFlag.CompanyIntelligence)),
                    new ResponseRatesConfigResponse(featureFlags.IsEnabled(FeatureFlag.ResponseRates)),
                    new SilenceReportsConfigResponse(featureFlags.IsEnabled(FeatureFlag.SilenceReports)),
                    new BoardConfigResponse(featureFlags.IsEnabled(FeatureFlag.Board))));
            })
            .WithTags("Config")
            .WithSummary("Public client configuration")
            .WithDescription("The server-side limits a client should show the user up front: the password policy " +
                              "(what register and reset-password enforce), the personal-access-token limits, and whether " +
                              "Sign in with Google/LinkedIn/GitHub are available (plus their public client ids), "
                              + "and whether the CV scan can offer its optional content-notes consent. " +
                              "Anonymous; nothing here is secret. All of it is still enforced server-side.")
            .Produces<ClientConfigResponse>();

        return app;
    }
}
