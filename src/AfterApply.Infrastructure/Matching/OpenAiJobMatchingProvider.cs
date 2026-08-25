using System.ClientModel;
using System.Text.Json;
using AfterApply.Application.Matching;
using AfterApply.Application.Common;
using AfterApply.Domain.Matching;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AfterApply.Infrastructure.Matching;

/// <summary>Real OpenAI implementation of IJobMatchingProvider (spec §12). Uses structured JSON
/// output (a strict JSON schema) instead of free-form text parsing, so the model's response maps
/// directly onto JobMatchProviderResult without a separate, failure-prone text-parsing step.</summary>
internal sealed class OpenAiJobMatchingProvider(IOptions<OpenAiOptions> options) : IJobMatchingProvider
{
    private const string SystemPrompt =
        "You are a recruiting assistant that compares a candidate's CV against a job description. " +
        "Score how well the candidate matches the job from 0 to 100. List the candidate's strongest " +
        "matching skills/qualifications and the most important skills/qualifications the job asks for " +
        "that are missing from the CV. Recommend Apply when the match is strong, Consider when it's " +
        "partial, or Skip when the match is weak. Be concise: short skill/qualification phrases, not sentences.";

    private static readonly BinaryData ResponseSchema = BinaryData.FromBytes([
        .. """
           {
             "type": "object",
             "properties": {
               "score": { "type": "integer", "minimum": 0, "maximum": 100 },
               "strongMatches": { "type": "array", "items": { "type": "string" } },
               "missing": { "type": "array", "items": { "type": "string" } },
               "recommendation": { "type": "string", "enum": ["Apply", "Consider", "Skip"] }
             },
             "required": ["score", "strongMatches", "missing", "recommendation"],
             "additionalProperties": false
           }
           """u8
    ]);

    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<JobMatchProviderResult> MatchAsync(string cvText, string jobDescription, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new CodedException("MATCHING_PROVIDER_NOT_CONFIGURED",
                "OpenAI is not configured. Set OpenAI:ApiKey.");
        }

        var client = new ChatClient(options.Value.Model, apiKey);

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "job_match_result", ResponseSchema, jsonSchemaIsStrict: true)
        };

        ChatCompletion completion;
        try
        {
            completion = await client.CompleteChatAsync(
                [
                    new SystemChatMessage(SystemPrompt),
                    new UserChatMessage($"CV:\n{cvText}\n\nJob description:\n{jobDescription}")
                ],
                chatOptions, cancellationToken);
        }
        catch (ClientResultException ex)
        {
            throw new CodedException("MATCHING_PROVIDER_ERROR", $"OpenAI request failed: {ex.Message}");
        }

        var payload = JsonSerializer.Deserialize<JobMatchPayload>(completion.Content[0].Text, PayloadJsonOptions)
            ?? throw new CodedException("MATCHING_PROVIDER_ERROR", "OpenAI returned an empty response.");

        return new JobMatchProviderResult(
            Math.Clamp(payload.Score, 0, 100),
            payload.StrongMatches,
            payload.Missing,
            Enum.Parse<JobMatchRecommendation>(payload.Recommendation));
    }

    private sealed record JobMatchPayload(int Score, IReadOnlyList<string> StrongMatches, IReadOnlyList<string> Missing, string Recommendation);
}
