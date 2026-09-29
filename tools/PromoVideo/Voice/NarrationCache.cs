using System.Security.Cryptography;
using System.Text;

namespace PromoVideo.Voice;

/// <summary>
/// Synthesised narration, kept by (voice fingerprint, text). Re-rendering after a UI change or a
/// timing tweak reuses every clip whose words did not change, so a paid voice is paid for once.
/// </summary>
internal sealed class NarrationCache(string directory, ITextToSpeech voice)
{
    public async Task<short[]> GetAsync(string text, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(voice.Fingerprint + "\n" + text)));
        var cached = Path.Combine(directory, key + ".pcm");

        if (!File.Exists(cached))
        {
            // say writes AIFF, the others WAV; ffmpeg reads either by content.
            var raw = Path.Combine(directory, key + (voice is SayVoice ? ".aiff" : ".wav"));
            await voice.SynthesizeAsync(text, raw, cancellationToken);
            try
            {
                var samples = await Processes.DecodeAudioAsync(raw, cancellationToken);
                var bytes = new byte[samples.Length * 2];
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                await File.WriteAllBytesAsync(cached, bytes, cancellationToken);
            }
            finally
            {
                File.Delete(raw);
            }
        }

        var data = await File.ReadAllBytesAsync(cached, cancellationToken);
        var result = new short[data.Length / 2];
        Buffer.BlockCopy(data, 0, result, 0, result.Length * 2);
        return result;
    }
}
