using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

// Proves Companies:FuzzyMatchThreshold is actually read from configuration, not hard-coded —
// same pair-classification as CompanyIntelligenceOptions' own "not hard-coded" test convention
// (DECISIONS.md Sprint 10). Own host profile (rather than reusing
// ExtensionApplicationTests') specifically so it can override this one setting without affecting
// the other tests' default-threshold behavior.
public sealed class CompanyAutoAttachThresholdProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // A near-1.0 threshold means even a one-character typo (the same fixture used by
        // ExtensionApplicationTests' default-threshold test) no longer clears it.
        builder.UseSetting("Companies:FuzzyMatchThreshold", "0.99");
    }
}

[Collection(IntegrationTestCollection.Name)]
public class CompanyAutoAttachThresholdTests(ApiHost<CompanyAutoAttachThresholdProfile> host) : IClassFixture<ApiHost<CompanyAutoAttachThresholdProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("threshold.test@example.com", "P@ssw0rd123!", "Threshold", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Raised_Threshold_Prevents_Auto_Attach_For_A_Match_That_Would_Otherwise_Qualify()
    {
        var seedResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Nova Yazilim", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null),
            JsonOptions);
        seedResponse.EnsureSuccessStatusCode();
        var seeded = await seedResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Nova Yazlim", "Frontend Engineer",
                "https://www.linkedin.com/jobs/view/3333333333/", null, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.CompanyId.ShouldNotBe(seeded!.CompanyId);
    }
}
