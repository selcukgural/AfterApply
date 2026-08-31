using System.ClientModel;
using System.Text.Json;
using AfterApply.Application.Common;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.Matching;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Real OpenAI implementation of IEmailClassificationProvider. Reuses OpenAiOptions (the
/// same API key/model as OpenAiJobMatchingProvider — no separate Email-specific key) and the same
/// structured-JSON-output pattern, so the model's response maps directly onto EmailClassificationResult
/// without free-form text parsing. Deliberately constrained to a closed status enum + "NoSignal" so
/// the model can't invent a status the domain doesn't have.</summary>
internal sealed class OpenAiEmailClassificationProvider(IOptions<OpenAiOptions> options) : IEmailClassificationProvider
{
    private const string SystemPrompt =
        "You classify a job application status-update email by its subject and snippet. Decide which " +
        "stage, if any, the email signals: Screening (a phone/recruiter screen is being scheduled), " +
        "Interview (a general interview invitation), TechnicalInterview (explicitly technical/coding), " +
        "FinalInterview (explicitly final round/onsite), Offer (a job offer, offer letter, or the " +
        "candidate's own reply accepting an offer counts as Accepted, not Offer), Accepted (the " +
        "candidate has been hired, or the candidate's own message accepts an offer), Rejected (the " +
        "application was declined — including emails that mention \"interview\" only to say one will " +
        "NOT be offered, which is still Rejected, not Interview), or NoSignal (anything else — " +
        "application-received acknowledgements, unrelated mail, or no clear status signal). Pay close " +
        "attention to negation: a sentence declining to invite someone to an interview is a rejection, " +
        "not an interview invitation. Be conservative — prefer NoSignal over guessing.";

    private static readonly BinaryData ResponseSchema = BinaryData.FromBytes([
        .. """
           {
             "type": "object",
             "properties": {
               "status": {
                 "type": "string",
                 "enum": ["Screening", "Interview", "TechnicalInterview", "FinalInterview", "Offer", "Accepted", "Rejected", "NoSignal"]
               },
               "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
             },
             "required": ["status", "confidence"],
             "additionalProperties": false
           }
           """u8
    ]);

    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<EmailClassificationResult> ClassifyAsync(string subject, string snippet, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new CodedException("EMAIL_CLASSIFICATION_PROVIDER_NOT_CONFIGURED",
                "OpenAI is not configured. Set OpenAI:ApiKey.");
        }

        var client = new ChatClient(options.Value.Model, apiKey);

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "email_classification_result", ResponseSchema, jsonSchemaIsStrict: true)
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
            throw new CodedException("EMAIL_CLASSIFICATION_PROVIDER_ERROR", $"OpenAI request failed: {ex.Message}");
        }

        var payload = JsonSerializer.Deserialize<EmailClassificationPayload>(completion.Content[0].Text, PayloadJsonOptions)
            ?? throw new CodedException("EMAIL_CLASSIFICATION_PROVIDER_ERROR", "OpenAI returned an empty response.");

        if (payload.Status == "NoSignal")
        {
            return new EmailClassificationResult(null, 0, "Llm:NoSignal");
        }

        var status = Enum.Parse<ApplicationStatus>(payload.Status);
        return new EmailClassificationResult(status, Math.Clamp(payload.Confidence, 0, 1), $"Llm:{payload.Status}");
    }

    private sealed record EmailClassificationPayload(string Status, double Confidence);
}
