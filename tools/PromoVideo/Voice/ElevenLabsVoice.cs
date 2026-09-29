using System.Globalization;
using System.Net.Http.Json;

namespace PromoVideo.Voice;

/// <summary>
/// ElevenLabs text-to-speech: very natural multilingual voices, Turkish included. Publishing needs
/// a paid plan — the free plan is non-commercial and asks for attribution — so check the current
/// plan terms before a video goes out.
///
/// The key comes from ELEVENLABS_API_KEY and is never written anywhere; a key limited to the
/// Text to Speech permission is enough. The voice is an ElevenLabs voice id, the model a model id.
/// </summary>
internal sealed class ElevenLabsVoice(string voiceId, string model, double rate) : ITextToSpeech
{
    public const string DefaultModel = "eleven_multilingual_v2";

    // Raw 16-bit mono PCM; 24 kHz is the highest PCM rate every plan gets.
    private const int PcmSampleRate = 24000;

    public string Fingerprint => $"elevenlabs|{voiceId}|{model}|{rate.ToString(CultureInfo.InvariantCulture)}";

    public async Task SynthesizeAsync(string text, string outputFile, CancellationToken cancellationToken)
    {
        var key = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Set ELEVENLABS_API_KEY to use the elevenlabs voice.");
        }

        using var client = new HttpClient { BaseAddress = new Uri("https://api.elevenlabs.io/v1/"), Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.Add("xi-api-key", key);
        using var response = await client.PostAsJsonAsync(
            $"text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format=pcm_{PcmSampleRate}",
            new { text, model_id = model, voice_settings = new { speed = rate } },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"ElevenLabs answered {(int)response.StatusCode}: {detail.Trim()}\n" +
                "Is the voice id right, and does the key have the Text to Speech permission?");
        }

        var pcm = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var samples = new short[pcm.Length / 2];
        Buffer.BlockCopy(pcm, 0, samples, 0, samples.Length * 2);
        await File.WriteAllBytesAsync(outputFile, Timeline.ToWav(samples, PcmSampleRate), cancellationToken);
    }
}
