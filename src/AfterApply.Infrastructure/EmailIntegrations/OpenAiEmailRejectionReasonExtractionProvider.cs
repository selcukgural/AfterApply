using System.ClientModel;
using System.Text.Json;
using AfterApply.Application.Common;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.OpenAi;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Real OpenAI implementation of IEmailRejectionReasonExtractionProvider. Same
/// structured-JSON-output pattern as OpenAiEmailClassificationProvider/OpenAiEmailJobExtractionProvider.
/// The NotStated bar (generic/probabilistic language doesn't count) is the load-bearing part of the
/// prompt — see the mailbox audit in DECISIONS.md that motivated it (Anthropic's "the most common
/// reason we don't move forward is..." disclaimer must NOT be tagged ExperienceLevelMismatch, since
/// it's sent to every rejected candidate regardless of their actual application).</summary>
internal sealed class OpenAiEmailRejectionReasonExtractionProvider(IOptions<OpenAiOptions> options)
    : IEmailRejectionReasonExtractionProvider
{
    private const string SystemPrompt =
        "You read a rejection email for a job application and decide whether it states a concrete, " +
        "personal reason for THIS candidate's rejection. Categories: LanguageRequirement (a specific " +
        "language fluency requirement wasn't met), LocationOrRelocation (a location/residency/work-" +
        "permit/relocation requirement wasn't met), ExperienceLevelMismatch (stated as too junior or " +
        "too senior/overqualified for this role), SalaryExpectationMismatch (compensation expectations " +
        "exceed the role's range), SkillOrTechStackGap (a specific named skill or technology is " +
        "missing), PositionCancelledOrFilled (the role was cancelled, paused, or filled by someone " +
        "else before/without a real comparison), CultureOrTeamFit (a team/culture fit reason is " +
        "stated), Other (a concrete reason is stated but doesn't fit any of the above — describe it " +
        "in detail), or NotStated. Use NotStated whenever the email gives no reason at all, or only " +
        "uses generic/probabilistic language about candidates in general rather than a direct claim " +
        "about this candidate specifically — for example \"we decided to move forward with other " +
        "candidates\", \"we received a high volume of applications\", or \"the most common reason we " +
        "don't move forward is X\" are NOT a stated reason for this candidate and must be NotStated. " +
        "Only pick a non-NotStated category when the email directly asserts something about this " +
        "candidate's application (e.g. \"this role requires Dutch at C1 level\", \"we're looking for " +
        "someone based in the Netherlands\"). Be conservative — prefer NotStated over guessing. When " +
        "not NotStated, set detail to a short quote or close paraphrase (under 200 characters) of the " +
        "sentence that states the reason.";

    private static readonly BinaryData ResponseSchema = BinaryData.FromBytes([
        .. """
           {
             "type": "object",
             "properties": {
               "category": {
                 "type": "string",
                 "enum": ["NotStated", "LanguageRequirement", "LocationOrRelocation", "ExperienceLevelMismatch", "SalaryExpectationMismatch", "SkillOrTechStackGap", "PositionCancelledOrFilled", "CultureOrTeamFit", "Other"]
               },
               "detail": { "type": ["string", "null"] },
               "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
             },
             "required": ["category", "detail", "confidence"],
             "additionalProperties": false
           }
           """u8
    ]);

    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<EmailRejectionReasonExtractionResult> ExtractAsync(string subject, string snippet, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new CodedException("EMAIL_REJECTION_REASON_PROVIDER_NOT_CONFIGURED",
                "OpenAI is not configured. Set OpenAI:ApiKey.");
        }

        var client = new ChatClient(options.Value.Model, apiKey);

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "email_rejection_reason_result", ResponseSchema, jsonSchemaIsStrict: true)
        };

        ChatCompletion completion;
        try
        {
            completion = await client.CompleteChatAsync(
                [
                    new SystemChatMessage(SystemPrompt),
                    new UserChatMessage($"Subject: {subject}\nSnippet: {snippet}")
                ],
                chatOptions, cancellationToken);
        }
        catch (ClientResultException ex)
        {
            throw new CodedException("EMAIL_REJECTION_REASON_PROVIDER_ERROR", $"OpenAI request failed: {ex.Message}");
        }

        var payload = JsonSerializer.Deserialize<EmailRejectionReasonPayload>(completion.Content[0].Text, PayloadJsonOptions)
            ?? throw new CodedException("EMAIL_REJECTION_REASON_PROVIDER_ERROR", "OpenAI returned an empty response.");

        var category = Enum.Parse<RejectionReasonCategory>(payload.Category);
        var detail = category == RejectionReasonCategory.NotStated || string.IsNullOrWhiteSpace(payload.Detail)
            ? null
            : payload.Detail.Trim();

        return new EmailRejectionReasonExtractionResult(category, detail, Math.Clamp(payload.Confidence, 0, 1));
    }

    private sealed record EmailRejectionReasonPayload(string Category, string? Detail, double Confidence);
}
