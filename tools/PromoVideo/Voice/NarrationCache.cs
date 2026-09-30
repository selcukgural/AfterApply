using System.Security.Cryptography;
using System.Text;

namespace PromoVideo.Voice;

/// <summary>
/// Synthesised narration, kept by (voice fingerprint, text). Re-rendering after a UI change or a
/// timing tweak reuses every clip whose words did not change, so a paid voice is paid for once.
/// </summary>
internal sealed class NarrationCache(string directory, ITextToSpeech voice)
{
    /// <summary>Takes asked for when a clip comes back cut mid-sound (see <see cref="Timeline.EndsAbruptly"/>).</summary>
    private const int MaxTakes = 3;

    public async Task<short[]> GetAsync(string text, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(voice.Fingerprint + "\n" + text)));
        var cached = Path.Combine(directory, key + ".pcm");

        // A clip cached before the cut check existed is checked too, so an old cut ending is replaced.
        if (File.Exists(cached) && !Timeline.EndsAbruptly(await ReadAsync(cached, cancellationToken)))
        {
            return await ReadAsync(cached, cancellationToken);
        }

        var best = await SynthesizeAsync(text, key, cancellationToken);
        for (var take = 2; take <= MaxTakes && Timeline.EndsAbruptly(best); take++)
        {
            Console.WriteLine($"  voice  ended mid-sound, take {take}: \"{Preview(text)}\"");
            var next = await SynthesizeAsync(text, key, cancellationToken);
            if (Timeline.TailPeak(next) < Timeline.TailPeak(best))
            {
                best = next;
            }
        }

        if (Timeline.EndsAbruptly(best))
        {
            Console.WriteLine($"  voice  still cut after {MaxTakes} takes, faded out: \"{Preview(text)}\"");
            best = Timeline.FadeOut(best);
        }

        var bytes = new byte[best.Length * 2];
        Buffer.BlockCopy(best, 0, bytes, 0, bytes.Length);
        await File.WriteAllBytesAsync(cached, bytes, cancellationToken);
        return best;
    }

    private async Task<short[]> SynthesizeAsync(string text, string key, CancellationToken cancellationToken)
    {
        // say writes AIFF, the others WAV; ffmpeg reads either by content.
        var raw = Path.Combine(directory, key + (voice is SayVoice ? ".aiff" : ".wav"));
        await voice.SynthesizeAsync(text, raw, cancellationToken);
        try
        {
            return await Processes.DecodeAudioAsync(raw, cancellationToken);
        }
        finally
        {
            File.Delete(raw);
        }
    }

    private static async Task<short[]> ReadAsync(string file, CancellationToken cancellationToken)
    {
        var data = await File.ReadAllBytesAsync(file, cancellationToken);
        var result = new short[data.Length / 2];
        Buffer.BlockCopy(data, 0, result, 0, result.Length * 2);
        return result;
    }

    private static string Preview(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
