using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PromoVideo;

/// <summary>Where one scene landed in the recording, in seconds from the video's first frame.</summary>
public sealed record SceneSpan(Scene Scene, double Start, double End, double VoiceStart, double VoiceSeconds);

/// <summary>One subtitle line on screen.</summary>
public sealed record Caption(double Start, double End, string Text);

/// <summary>The pure half of a render: how long a scene must last, how the narration is cut into
/// subtitle lines, the SRT and chapter texts, and where each voice clip goes in the audio track.</summary>
public static partial class Timeline
{
    public const int SampleRate = 48000;

    /// <summary>Two subtitle lines of about 42 characters — the usual readable maximum.</summary>
    public const int MaxCaptionLength = 84;

    /// <summary>A clip whose last 20 ms still peak above this (about -30 dBFS) was cut mid-sound:
    /// Gemini-TTS sometimes returns a clip that stops inside the last syllable.</summary>
    public const double AbruptEndPeak = 0.03;

    /// <summary>The loudest sample of the clip's last <paramref name="milliseconds"/>, 0–1 of full scale.</summary>
    public static double TailPeak(short[] samples, int milliseconds = 20)
    {
        var count = Math.Min(samples.Length, SampleRate * milliseconds / 1000);
        var peak = 0;
        for (var i = samples.Length - count; i < samples.Length; i++)
        {
            peak = Math.Max(peak, Math.Abs((int)samples[i]));
        }

        return peak / 32768.0;
    }

    public static bool EndsAbruptly(short[] samples) => TailPeak(samples) > AbruptEndPeak;

    /// <summary>A copy with the last <paramref name="milliseconds"/> faded to silence — the fallback
    /// when every take ends mid-sound, so the cut at least doesn't click.</summary>
    public static short[] FadeOut(short[] samples, int milliseconds = 40)
    {
        var result = (short[])samples.Clone();
        var count = Math.Min(result.Length, SampleRate * milliseconds / 1000);
        for (var i = 0; i < count; i++)
        {
            var index = result.Length - count + i;
            result[index] = (short)(result[index] * (1 - (i + 1) / (double)count));
        }

        return result;
    }

    /// <summary>How long a scene has to last: its voice plus the pauses around it, or its minimum.</summary>
    public static double SceneSeconds(Scene scene, double voiceSeconds) =>
        Math.Max(voiceSeconds > 0 ? scene.LeadIn + voiceSeconds + scene.Tail : 0, scene.MinSeconds ?? 0);

    /// <summary>The narration cut into sentences, and long sentences into pieces at word boundaries.</summary>
    public static IReadOnlyList<string> SplitNarration(string? narration)
    {
        if (string.IsNullOrWhiteSpace(narration))
        {
            return [];
        }

        var pieces = new List<string>();
        foreach (var sentence in SentenceBoundary().Split(Whitespace().Replace(narration.Trim(), " ")))
        {
            var remaining = sentence.Trim();
            while (remaining.Length > MaxCaptionLength)
            {
                var cut = remaining.LastIndexOf(' ', MaxCaptionLength);
                if (cut <= 0)
                {
                    cut = MaxCaptionLength;
                }

                pieces.Add(remaining[..cut].Trim());
                remaining = remaining[cut..].Trim();
            }

            if (remaining.Length > 0)
            {
                pieces.Add(remaining);
            }
        }

        return pieces;
    }

    /// <summary>Subtitle lines for one scene, spread over the time its voice speaks, each line's
    /// share proportional to its length — close to how a steady voice actually paces.</summary>
    public static IReadOnlyList<Caption> Captions(SceneSpan span)
    {
        var pieces = SplitNarration(span.Scene.Narration);
        if (pieces.Count == 0 || span.VoiceSeconds <= 0)
        {
            return [];
        }

        var total = pieces.Sum(p => p.Length);
        var captions = new List<Caption>(pieces.Count);
        var at = span.VoiceStart;
        foreach (var piece in pieces)
        {
            var length = span.VoiceSeconds * piece.Length / total;
            captions.Add(new Caption(at, at + length, piece));
            at += length;
        }

        return captions;
    }

    public static string ToSrt(IEnumerable<Caption> captions)
    {
        var builder = new StringBuilder();
        var index = 1;
        foreach (var caption in captions)
        {
            builder.Append(index++.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append(SrtTime(caption.Start)).Append(" --> ").Append(SrtTime(caption.End)).Append('\n')
                .Append(caption.Text).Append("\n\n");
        }

        return builder.ToString();
    }

    public static string SrtTime(double seconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Round(Math.Max(0, seconds) * 1000));
        return string.Create(CultureInfo.InvariantCulture,
            $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}");
    }

    /// <summary>
    /// The chapter list for the YouTube description, plus what would stop YouTube from showing it:
    /// it wants the first chapter at 00:00, at least three chapters, each at least ten seconds.
    /// </summary>
    public static (string Text, IReadOnlyList<string> Warnings) Chapters(IReadOnlyList<SceneSpan> spans)
    {
        var chapters = new List<(double Start, string Title)>();
        foreach (var span in spans)
        {
            if (!string.IsNullOrWhiteSpace(span.Scene.Chapter))
            {
                chapters.Add((chapters.Count == 0 ? 0 : span.Start, span.Scene.Chapter.Trim()));
            }
        }

        var warnings = new List<string>();
        if (chapters.Count == 0)
        {
            return (string.Empty, warnings);
        }

        if (chapters.Count < 3)
        {
            warnings.Add("YouTube shows chapters only when there are at least three.");
        }

        var end = spans.Count > 0 ? spans[^1].End : 0;
        for (var i = 0; i < chapters.Count; i++)
        {
            var next = i + 1 < chapters.Count ? chapters[i + 1].Start : end;
            if (next - chapters[i].Start < 10)
            {
                warnings.Add($"Chapter '{chapters[i].Title}' is shorter than the 10 seconds YouTube requires.");
            }
        }

        var text = string.Join('\n', chapters.Select(c => $"{ChapterTime(c.Start)} {c.Title}"));
        return (text, warnings);
    }

    public static string ChapterTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Floor(Math.Max(0, seconds)));
        return time.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{time.Minutes:00}:{time.Seconds:00}");
    }

    /// <summary>
    /// The narration track: every clip (16-bit mono PCM at <see cref="SampleRate"/>) laid at its
    /// offset in a silent buffer of the given length. Clips never overlap in practice — a scene
    /// lasts at least as long as its voice — but where they would, they are summed and clipped
    /// rather than one overwriting the other.
    /// </summary>
    public static short[] Mix(double totalSeconds, IEnumerable<(double Offset, short[] Samples)> clips)
    {
        var buffer = new int[(int)Math.Ceiling(Math.Max(0, totalSeconds) * SampleRate)];
        foreach (var (offset, samples) in clips)
        {
            var start = (int)Math.Round(Math.Max(0, offset) * SampleRate);
            for (var i = 0; i < samples.Length && start + i < buffer.Length; i++)
            {
                buffer[start + i] += samples[i];
            }
        }

        return buffer.Select(v => (short)Math.Clamp(v, short.MinValue, short.MaxValue)).ToArray();
    }

    public static double Seconds(short[] samples) => (double)samples.Length / SampleRate;

    /// <summary>A mono 16-bit PCM WAV file around the samples.</summary>
    public static byte[] ToWav(short[] samples, int sampleRate = SampleRate)
    {
        var dataBytes = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// The ffmpeg filter and H.264 settings for the picture, following YouTube's upload
    /// recommendations: progressive High profile, closed GOP of half the frame rate, two B-frames,
    /// and 4:2:0 in limited ("tv") range tagged BT.709. The screencast frames are full-range BT.601
    /// JPEGs; without the explicit conversion the file keeps a full-range tag (yuvj420p) that
    /// YouTube's re-encode can read as washed-out or crushed colours.
    /// </summary>
    public static IReadOnlyList<string> VideoEncodeArguments(OutputSettings output)
    {
        var size = string.Create(CultureInfo.InvariantCulture, $"{output.Width}:{output.Height}");
        var fps = output.Fps.ToString(CultureInfo.InvariantCulture);
        return
        [
            "-vf", $"fps={fps},scale={size}:force_original_aspect_ratio=decrease:flags=lanczos" +
                   ":in_range=pc:out_range=tv:in_color_matrix=bt601:out_color_matrix=bt709" +
                   $",pad={size}:(ow-iw)/2:(oh-ih)/2:color=white,format=yuv420p" +
                   // Output flags alone leave primaries/transfer unset; setparams tags the frames themselves.
                   ",setparams=range=tv:colorspace=bt709:color_primaries=bt709:color_trc=bt709",
            "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-profile:v", "high",
            "-g", Math.Max(1, output.Fps / 2).ToString(CultureInfo.InvariantCulture), "-bf", "2", "-flags", "+cgop",
            "-r", fps
        ];
    }

    /// <summary>The ffconcat list that turns screencast frames (which arrive only when the screen
    /// changes) into a timeline: each frame is shown until the next one arrived.</summary>
    public static string FrameList(IReadOnlyList<(string File, double At)> frames, double end)
    {
        var builder = new StringBuilder("ffconcat version 1.0\n");
        for (var i = 0; i < frames.Count; i++)
        {
            var until = i + 1 < frames.Count ? frames[i + 1].At : Math.Max(end, frames[i].At + 0.04);
            var duration = Math.Max(0.001, until - frames[i].At);
            builder.Append("file '").Append(frames[i].File).Append("'\n")
                .Append("duration ").Append(duration.ToString("0.######", CultureInfo.InvariantCulture)).Append('\n');
        }

        // The concat demuxer ignores the last entry's duration unless the file is listed again.
        if (frames.Count > 0)
        {
            builder.Append("file '").Append(frames[^1].File).Append("'\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// The audio chain: the voice alone, or the voice over music that ducks whenever the voice
    /// speaks (sidechain compression) — so the music fills the pauses and never competes with the
    /// words. Everything ends normalised to −16 LUFS, near what YouTube plays at, with −1.5 dB
    /// true-peak headroom.
    /// </summary>
    public static string AudioFilter(double? musicVolume)
    {
        const string loudness = "loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000";
        if (musicVolume is not { } volume)
        {
            return $"[1:a]{loudness}[a]";
        }

        var level = volume.ToString("0.###", CultureInfo.InvariantCulture);
        return $"[2:a]volume={level}[m];[1:a]asplit=2[sc][vo];" +
               "[m][sc]sidechaincompress=threshold=0.02:ratio=6:attack=40:release=700[md];" +
               $"[vo][md]amix=inputs=2:duration=first:normalize=0,{loudness}[a]";
    }

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex SentenceBoundary();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
