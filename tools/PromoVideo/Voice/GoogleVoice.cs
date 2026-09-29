using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PromoVideo.Voice;

/// <summary>
/// Google Cloud Text-to-Speech, the voice meant for published videos: natural Turkish and English
/// voices, commercial use allowed under the Cloud terms, and a free monthly character quota that a
/// few promo videos stay well inside (check the current numbers on the pricing page).
///
/// Authenticates with the gcloud CLI's own credentials (<c>gcloud auth print-access-token</c>), so no
/// key file is created or stored; the project to bill comes from GOOGLE_CLOUD_PROJECT or gcloud's
/// configured project.
/// </summary>
/// <remarks>
/// With a <c>model</c> (e.g. gemini-2.5-pro-tts) the voice is one of the Gemini-TTS voices ("Charon",
/// "Kore", ...), which read from the sentence's meaning — abbreviations with suffixes included — and
/// take a plain-language style <c>prompt</c>. Without one, the name is a full voice name such as
/// tr-TR-Chirp3-HD-Charon.
/// </remarks>
internal sealed class GoogleVoice(string voice, string languageCode, double rate, string? model = null, string? prompt = null) : ITextToSpeech
{
    private const string Endpoint = "https://texttospeech.googleapis.com/v1/";

    // A Chirp request carries no model or prompt at all rather than explicit nulls.
    private static readonly JsonSerializerOptions OmitNulls =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    // The model and the prompt only join the key when set, so the Chirp clips already cached keep theirs.
    public string Fingerprint => $"google|{voice}|{rate.ToString(CultureInfo.InvariantCulture)}"
                                 + (model is null ? "" : $"|{model}|{prompt}");

    public async Task SynthesizeAsync(string text, string outputFile, CancellationToken cancellationToken)
    {
        using var client = await CreateClientAsync(cancellationToken);
        // "./" matters: a bare "text:synthesize" parses as a URI whose scheme is "text".
        using var response = await client.PostAsJsonAsync("./text:synthesize", new
        {
            input = new { text, prompt },
            voice = new { languageCode, name = voice, model_name = model },
            audioConfig = new { audioEncoding = "LINEAR16", sampleRateHertz = Timeline.SampleRate, speakingRate = rate }
        }, OmitNulls, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        // LINEAR16 comes back as a complete WAV file, header included.
        await File.WriteAllBytesAsync(outputFile, body.RootElement.GetProperty("audioContent").GetBytesFromBase64(), cancellationToken);
    }

    /// <summary>The voice names available for a language, for choosing one.</summary>
    public static async Task<IReadOnlyList<string>> ListAsync(string languageCode, CancellationToken cancellationToken)
    {
        using var client = await CreateClientAsync(cancellationToken);
        using var response = await client.GetAsync($"voices?languageCode={Uri.EscapeDataString(languageCode)}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return body.RootElement.TryGetProperty("voices", out var voices)
            ? voices.EnumerateArray()
                .Select(v => $"{v.GetProperty("name").GetString()}  ({v.GetProperty("ssmlGender").GetString()})")
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
    }

    private static async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var token = Encoding.UTF8.GetString(await Processes.RunAsync("gcloud", ["auth", "print-access-token"], cancellationToken)).Trim();
        var project = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        if (string.IsNullOrWhiteSpace(project))
        {
            project = Encoding.UTF8.GetString(await Processes.RunAsync("gcloud", ["config", "get-value", "project"], cancellationToken)).Trim();
        }

        var client = new HttpClient { BaseAddress = new Uri(Endpoint), Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrWhiteSpace(project))
        {
            client.DefaultRequestHeaders.Add("x-goog-user-project", project);
        }

        return client;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Google Text-to-Speech answered {(int)response.StatusCode}: {detail.Trim()}\n" +
                "Is the Text-to-Speech API enabled on the project, and is the voice name right? List voices with the 'voices' command.");
        }
    }
}
