using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyReviews;

/// <summary>CompanyReviews:Enabled=false — the feature deployed dark. Every review route is a 404
/// for everyone, admin included, and /api/config says so.</summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanyReviewsDisabledTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CompanyReviewsDisabledTests));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("CompanyReviews:Enabled", "false");
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await TestHostDisposal.DisposeQuietlyAsync(_factory);
        }
    }

    [Fact]
    public async Task Every_Route_Is_Not_Found_And_Config_Reports_The_Flag()
    {
        var client = _factory!.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dark.reviews@example.com", "P@ssw0rd123!", "Dark", "Launch", true), JsonOptions);
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        (await client.GetAsync("/api/companies/public")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/companies/public/anything")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/company-reviews/mine")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest("Dark Co"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/admin/company-reviews/counts")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var config = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        config!.CompanyReviews.ShouldNotBeNull();
        config.CompanyReviews!.Enabled.ShouldBeFalse();
    }
}
