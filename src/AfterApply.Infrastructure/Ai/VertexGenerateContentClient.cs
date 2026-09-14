using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace AfterApply.Infrastructure.Ai;

/// <summary>
/// One <c>generateContent</c> call to Vertex AI (Gemini) over the REST endpoint of a pinned
/// region, shared by every feature that talks to the model. Two things about it are privacy
/// decisions rather than engineering ones, and both are visible here on purpose:
/// <list type="bullet">
/// <item><b>The region is in the URL.</b> The host is <c>{location}-aiplatform.googleapis.com</c>,
/// so a reader can see where a CV's text goes without consulting a console. Callers pin an EU
/// region, which is where the rest of this product's data already lives — that is what lets the
/// privacy policy say no new country is involved.</item>
/// <item><b>The caller is us, not a key.</b> Authentication is Application Default Credentials —
/// the Cloud Run runtime service account — so there is no API key to leak, rotate or accidentally
/// log, and access is revoked by removing an IAM binding.</item>
/// </list>
///
/// The REST endpoint is called directly rather than through Google.Cloud.AIPlatform.V1: this
/// needs exactly one request shape, and the gRPC package carries the generated surface of all of
/// Vertex for it. What is lost is typed request objects; what is kept is a dependency small enough
/// that the request body below is the whole contract.
///
/// On a non-success status the exception carries the status and nothing else: a Vertex error body
/// can echo part of the request, and the request is somebody's CV — it must not reach a log line
/// or Sentry.
/// </summary>
public interface IVertexGenerateContentClient
{
    Task<VertexGenerateContentResult> GenerateAsync(VertexGenerateContentCall call, CancellationToken cancellationToken);
}

internal sealed class VertexGenerateContentClient(IHttpClientFactory httpClientFactory) : IVertexGenerateContentClient
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VertexGenerateContentResult> GenerateAsync(VertexGenerateContentCall call, CancellationToken cancellationToken)
    {
        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = call.SystemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = call.UserText } } } },
            generationConfig = new
            {
                temperature = call.Temperature,
                maxOutputTokens = call.MaxOutputTokens,
                responseMimeType = "application/json",
                responseSchema = call.ResponseSchema
            }
        };

        var client = httpClientFactory.CreateClient(call.HttpClientName);
        client.Timeout = call.Timeout;

        var url = $"https://{call.Location}-aiplatform.googleapis.com/v1/projects/{call.ProjectId}" +
                  $"/locations/{call.Location}/publishers/google/models/{call.Model}:generateContent";

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Content = JsonContent.Create(payload);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new VertexGenerateContentException((int)response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(ResponseJsonOptions, cancellationToken);
        var text = body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        return new VertexGenerateContentResult(
            string.IsNullOrWhiteSpace(text) ? null : text,
            body?.UsageMetadata?.PromptTokenCount ?? 0,
            body?.UsageMetadata?.CandidatesTokenCount ?? 0);
    }

    /// <summary>
    /// The ambient credential, resolved once per process. Application Default Credentials means a
    /// file read or a metadata-server call to discover, which is not something to repeat per call;
    /// the credential object then caches and refreshes the access token itself, so
    /// <see cref="GetAccessTokenAsync"/> is a memory read for all but the first call of a token's
    /// lifetime.
    /// </summary>
    private static readonly Lazy<Task<GoogleCredential>> Credential = new(async () =>
    {
        var credential = await GoogleCredential.GetApplicationDefaultAsync();
        return credential.IsCreateScopedRequired
            ? credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform")
            : credential;
    });

    private static async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var credential = await Credential.Value;
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }

    private sealed record GenerateContentResponse(List<Candidate>? Candidates, UsageMetadata? UsageMetadata);

    private sealed record Candidate(Content? Content);

    private sealed record Content(List<Part>? Parts);

    private sealed record Part(string? Text);

    private sealed record UsageMetadata(int? PromptTokenCount, int? CandidatesTokenCount);
}

public sealed record VertexGenerateContentCall(
    string HttpClientName,
    string ProjectId,
    string Location,
    string Model,
    string SystemPrompt,
    string UserText,
    object ResponseSchema,
    double Temperature,
    int MaxOutputTokens,
    TimeSpan Timeout);

/// <summary>The model's JSON text (null when the answer was empty or filtered) and the token
/// counts Vertex reported for the call.</summary>
public sealed record VertexGenerateContentResult(string? Text, int InputTokens, int OutputTokens);

public sealed class VertexGenerateContentException(int statusCode) : Exception($"Vertex AI returned {statusCode}.")
{
    public int StatusCode { get; } = statusCode;
}
