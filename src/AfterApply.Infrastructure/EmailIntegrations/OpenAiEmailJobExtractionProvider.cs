using System.ClientModel;
using System.Text.Json;
using AfterApply.Application.Common;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Infrastructure.OpenAi;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Real OpenAI implementation of IEmailJobExtractionProvider. Reuses OpenAiOptions (same
/// API key/model as OpenAiEmailClassificationProvider) and the same structured-JSON-output pattern.
/// Deliberately separate from classification (single-responsibility port, same split as
/// RuleBasedEmailClassifier vs EmailApplicationMatcher) even though both are only ever called
/// back-to-back for the same email in EmailForwardingService.</summary>
internal sealed class OpenAiEmailJobExtractionProvider(IOptions<OpenAiOptions> options) : IEmailJobExtractionProvider
{
    private const string SystemPrompt =
        "You read a forwarded email that may be a company's reply to a job application (an interview " +
        "invitation, rejection, offer, or status update). Extract the company name and job title if, " +
        "and only if, you are confident this is a genuine job-application-related email and you can " +
        "identify both. Also extract the job's location and a short description of the role if the " +
        "email mentions them (both optional — leave null if absent). Set confident to false if the " +
        "email doesn't look like a job-application reply, or if you cannot identify both a company " +
        "name and a job title with reasonable certainty — never guess or invent either one.";

    private static readonly BinaryData ResponseSchema = BinaryData.FromBytes([
        .. """
           {
             "type": "object",
             "properties": {
               "confident": { "type": "boolean" },
               "companyName": { "type": ["string", "null"] },
               "jobTitle": { "type": ["string", "null"] },
               "location": { "type": ["string", "null"] },
               "description": { "type": ["string", "null"] }
             },
             "required": ["confident", "companyName", "jobTitle", "location", "description"],
             "additionalProperties": false
           }
           """u8
    ]);

    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<EmailJobExtractionResult?> ExtractAsync(string subject, string snippet, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new CodedException("EMAIL_EXTRACTION_PROVIDER_NOT_CONFIGURED",
                "OpenAI is not configured. Set OpenAI:ApiKey.");
        }

        var client = new ChatClient(options.Value.Model, apiKey);

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "email_job_extraction_result", ResponseSchema, jsonSchemaIsStrict: true)
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
            throw new CodedException("EMAIL_EXTRACTION_PROVIDER_ERROR", $"OpenAI request failed: {ex.Message}");
        }

        var payload = JsonSerializer.Deserialize<EmailJobExtractionPayload>(completion.Content[0].Text, PayloadJsonOptions)
            ?? throw new CodedException("EMAIL_EXTRACTION_PROVIDER_ERROR", "OpenAI returned an empty response.");

        if (!payload.Confident || string.IsNullOrWhiteSpace(payload.CompanyName) || string.IsNullOrWhiteSpace(payload.JobTitle))
        {
            return null;
        }

        return new EmailJobExtractionResult(payload.CompanyName.Trim(), payload.JobTitle.Trim(),
            string.IsNullOrWhiteSpace(payload.Location) ? null : payload.Location.Trim(),
            string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim());
    }

    private sealed record EmailJobExtractionPayload(bool Confident, string? CompanyName, string? JobTitle, string? Location, string? Description);
}
