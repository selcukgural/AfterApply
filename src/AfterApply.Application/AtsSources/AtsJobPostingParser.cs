using System.Globalization;
using System.Text.Json;
using AfterApply.Application.Common;
using AfterApply.Domain.Common;

namespace AfterApply.Application.AtsSources;

/// <summary>
/// Maps each ATS's own JSON onto <see cref="AtsJobPosting"/>. Written against live responses
/// (2026-09-22) rather than from documentation, because four of the five publish no schema; the
/// fixtures in <c>AtsJobPostingParserTests</c> are trimmed copies of those responses.
///
/// Everything is read defensively: a missing property, a null, a renamed field or a changed type
/// yields null for that one value rather than an exception. These are third-party APIs nobody
/// notifies us before changing, and the caller treats an empty posting as "nothing to add", which
/// is exactly the right outcome when a response stops making sense.
/// </summary>
public static class AtsJobPostingParser
{
    public static AtsJobPosting? Parse(Source source, string json, string externalId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return source switch
            {
                Source.Greenhouse => Greenhouse(root),
                Source.Lever => Lever(root),
                Source.Ashby => Ashby(root, externalId),
                Source.SmartRecruiters => SmartRecruiters(root),
                Source.Workday => Workday(root),
                Source.Workable => Workable(root, externalId),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // {"title","content" (HTML, entity-escaped a second time),"location":{"name"},"first_published"}
    private static AtsJobPosting Greenhouse(JsonElement root)
    {
        // Greenhouse escapes the markup itself, so "content" arrives as "&lt;p&gt;...". Decoding
        // once gives the HTML; HtmlToPlainText decodes the entities inside it afterwards.
        var html = System.Net.WebUtility.HtmlDecode(Text(root, "content"));

        return new AtsJobPosting(
            Title: Text(root, "title"),
            Description: HtmlToPlainText.Convert(html),
            DescriptionHtml: html,
            Location: Text(Child(root, "location"), "name"),
            PublishedAt: Timestamp(root, "first_published") ?? Timestamp(root, "updated_at"));
    }

    // {"text","description" (HTML),"descriptionPlain","categories":{"location","commitment"},
    //  "createdAt" (epoch ms)}
    private static AtsJobPosting Lever(JsonElement root)
    {
        var categories = Child(root, "categories");

        return new AtsJobPosting(
            Title: Text(root, "text"),
            Description: Text(root, "descriptionPlain") ?? HtmlToPlainText.Convert(Text(root, "description")),
            DescriptionHtml: Text(root, "description"),
            Location: Text(categories, "location"),
            PublishedAt: EpochMillis(root, "createdAt"),
            EmploymentType: MapEmploymentType(Text(categories, "commitment")));
    }

    // Ashby publishes the whole board, not a single posting, so the row is picked out by the uuid
    // half of the external id. A board that no longer lists it (filled, unpublished) simply yields
    // nothing, which is the correct outcome — we do not want to rewrite a closed job's fields.
    private static AtsJobPosting? Ashby(JsonElement root, string externalId)
    {
        var postingId = externalId.Split('/').ElementAtOrDefault(1);
        var jobs = Child(root, "jobs");
        if (postingId is null || jobs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var job in jobs.EnumerateArray())
        {
            if (!string.Equals(Text(job, "id"), postingId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new AtsJobPosting(
                Title: Text(job, "title"),
                Description: Text(job, "descriptionPlain") ?? HtmlToPlainText.Convert(Text(job, "descriptionHtml")),
                DescriptionHtml: Text(job, "descriptionHtml"),
                Location: Text(job, "location"),
                PublishedAt: Timestamp(job, "publishedAt"),
                EmploymentType: MapEmploymentType(Text(job, "employmentType")));
        }

        return null;
    }

    // {"name","location":{"fullLocation"},"releasedDate","typeOfEmployment":{"label"},
    //  "jobAd":{"sections":{"companyDescription","jobDescription","qualifications":{"text"}}}}
    private static AtsJobPosting SmartRecruiters(JsonElement root)
    {
        var sections = Child(Child(root, "jobAd"), "sections");
        var html = string.Join('\n', new[] { "companyDescription", "jobDescription", "qualifications", "additionalInformation" }
            .Select(name => Text(Child(sections, name), "text"))
            .Where(text => !string.IsNullOrWhiteSpace(text)));

        return new AtsJobPosting(
            Title: Text(root, "name"),
            Description: HtmlToPlainText.Convert(html),
            DescriptionHtml: string.IsNullOrWhiteSpace(html) ? null : html,
            Location: Text(Child(root, "location"), "fullLocation"),
            PublishedAt: Timestamp(root, "releasedDate"),
            EmploymentType: MapEmploymentType(Text(Child(root, "typeOfEmployment"), "label")));
    }

    // {"jobPostingInfo":{"title","location","jobDescription" (HTML),"startDate","timeType"}}
    private static AtsJobPosting? Workday(JsonElement root)
    {
        var info = Child(root, "jobPostingInfo");
        if (info.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AtsJobPosting(
            Title: Text(info, "title"),
            Description: HtmlToPlainText.Convert(Text(info, "jobDescription")),
            DescriptionHtml: Text(info, "jobDescription"),
            Location: Text(info, "location"),
            PublishedAt: Timestamp(info, "startDate"),
            EmploymentType: MapEmploymentType(Text(info, "timeType")));
    }

    // {"name","jobs":[{"shortcode","title","description" (HTML),"city","state","country",
    //  "employment_type","published_on"}]} — the board, like Ashby's, so the row is picked out by
    // the shortcode half of the external id. A board that no longer lists it yields nothing, which
    // is right: a closed job's stored fields should not be rewritten from a page that dropped it.
    private static AtsJobPosting? Workable(JsonElement root, string externalId)
    {
        var shortcode = externalId.Split('/').ElementAtOrDefault(1);
        var jobs = Child(root, "jobs");
        if (shortcode is null || jobs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var job in jobs.EnumerateArray())
        {
            if (!string.Equals(Text(job, "shortcode"), shortcode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var html = Text(job, "description");

            return new AtsJobPosting(
                Title: Text(job, "title"),
                Description: HtmlToPlainText.Convert(html),
                DescriptionHtml: html,
                // The row has no single location string, so it is assembled the way a person would
                // write it. The state sits between city and country only when there is no city to
                // carry the meaning ("Attica, Greece" says nothing "Athens, Greece" does not).
                Location: JoinLocation(Text(job, "city"), Text(job, "state"), Text(job, "country")),
                PublishedAt: Timestamp(job, "published_on") ?? Timestamp(job, "created_at"),
                EmploymentType: MapEmploymentType(Text(job, "employment_type")));
        }

        return null;
    }

    private static string? JoinLocation(string? city, string? state, string? country)
    {
        var parts = (string.IsNullOrWhiteSpace(city) ? new[] { state, country } : new[] { city, country })
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>The five write the same handful of concepts five ways ("FullTime", "Full-time",
    /// "Full time", "permanent"). Anything unrecognised is null, not a guess — a wrong employment
    /// type on someone's application record is worse than a blank one.</summary>
    private static EmploymentType? MapEmploymentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(char.IsLetter).ToArray()).ToLowerInvariant();

        return normalized switch
        {
            "fulltime" or "permanent" or "regular" => EmploymentType.FullTime,
            "parttime" => EmploymentType.PartTime,
            "contract" or "contractor" or "fixedterm" => EmploymentType.Contract,
            "intern" or "internship" or "traineeship" => EmploymentType.Internship,
            "freelance" => EmploymentType.Freelance,
            "temporary" or "temp" or "seasonal" => EmploymentType.Temporary,
            _ => null
        };
    }

    /// <summary>The one guarded property accessor every reader in this class goes through. A
    /// non-object parent (a response that became an array, or a field that became a string) yields
    /// <c>default</c> rather than throwing — System.Text.Json's own TryGetProperty throws on a
    /// non-object, which is how an unannounced schema change would otherwise become an
    /// exception.</summary>
    private static JsonElement Child(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var child)
            ? child
            : default;

    private static string? Text(JsonElement element, string name)
    {
        if (Child(element, name) is not { ValueKind: JsonValueKind.String } value)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static DateTimeOffset? Timestamp(JsonElement element, string name) =>
        Text(element, name) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static DateTimeOffset? EpochMillis(JsonElement element, string name) =>
        Child(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var millis)
            ? DateTimeOffset.FromUnixTimeMilliseconds(millis)
            : null;
}
