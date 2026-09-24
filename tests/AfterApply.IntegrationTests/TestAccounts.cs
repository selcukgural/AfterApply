using System.Net.Http.Json;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.IntegrationTests;

/// <summary>
/// Signed-in accounts for tests that are about something else. Since 2026-09-24 a sign-up gets no
/// tokens until its emailed code comes back; these helpers stand in for the inbox by marking the
/// address verified in the database — the one thing the code would do — and then sign in through
/// the real login endpoint. The verification flow itself is tested end to end in
/// EmailVerificationTests, code and all.
/// </summary>
public static class TestAccounts
{
    public static async Task<AuthResponse> RegisterVerifiedAsync(HttpClient client, IServiceProvider services, RegisterRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", request, ApiHost.JsonOptions);
        response.EnsureSuccessStatusCode();

        await MarkVerifiedAsync(services, request.Email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, request.Password), ApiHost.JsonOptions);
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponse>(ApiHost.JsonOptions))!;
    }

    public static async Task MarkVerifiedAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalized = email.ToUpperInvariant();
        await db.Users.Where(u => u.NormalizedEmail == normalized)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.EmailConfirmed, true));
    }

    public static bool IsVerificationCodeJob(PendingJob job) => job.Job.Type == typeof(IEmailVerificationCodeSender);
}
