namespace PromoVideo;

/// <summary>
/// A calm background bed the tool composes itself, so there is no licence to check and no
/// Content ID claim to fear: soft pads on a I–V–vi–IV progression in C (Cadd9, G/B, Am7, Fmaj7),
/// a quiet bass, and a slow plucked arpeggio. Deterministic — the same length gives the same
/// track. The renderer adds a low-pass and a short echo for room, then ducks it under the voice.
/// </summary>
public static class Music
{
    public const double ChordSeconds = 4.0;
    private const double Crossfade = 1.0;

    // MIDI notes: pad voicing and bass root per chord.
    private static readonly (int[] Pad, int Bass)[] Progression =
    [
        ([60, 64, 67, 74], 48), // Cadd9
        ([59, 62, 67, 71], 43), // G/B
        ([57, 60, 64, 67], 45), // Am7
        ([53, 57, 60, 64], 41)  // Fmaj7
    ];

    private static readonly int[] ArpeggioPattern = [0, 1, 2, 3, 2, 1, 2, 3];

    /// <summary>The ambient bed, mono at <see cref="Timeline.SampleRate"/>, peaking at 0.9 of full
    /// scale, fading in over 2 s and out over the last 3 s.</summary>
    public static short[] Ambient(double seconds)
    {
        var rate = Timeline.SampleRate;
        var length = (int)Math.Ceiling(Math.Max(0, seconds) * rate);
        var buffer = new double[length];
        var chords = (int)Math.Ceiling(seconds / ChordSeconds) + 1;

        for (var c = 0; c < chords; c++)
        {
            var (pad, bass) = Progression[c % Progression.Length];
            var start = c * ChordSeconds;

            foreach (var note in pad)
            {
                AddPad(buffer, start, Frequency(note), 0.16);
            }

            AddPad(buffer, start, Frequency(bass), 0.22, harmonics: false);

            // Eighth notes an octave up, quiet — movement without a melody to compete with the voice.
            var step = ChordSeconds / ArpeggioPattern.Length;
            for (var i = 0; i < ArpeggioPattern.Length; i++)
            {
                AddPluck(buffer, start + i * step, Frequency(pad[ArpeggioPattern[i]] + 12), 0.07);
            }
        }

        return ToPcm(buffer, fadeIn: 2, fadeOut: 3);
    }

    /// <summary>A track that is already audio (the scenario's own file), faded the same way.</summary>
    public static short[] Faded(short[] samples, double fadeIn = 2, double fadeOut = 3) =>
        ToPcm(samples.Select(s => s / 32768.0).ToArray(), fadeIn, fadeOut, normalize: false);

    public static double Frequency(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

    private static void AddPad(double[] buffer, double start, double frequency, double amplitude, bool harmonics = true)
    {
        var rate = Timeline.SampleRate;
        var from = (int)(start * rate);
        var to = Math.Min(buffer.Length, (int)((start + ChordSeconds + Crossfade) * rate));
        for (var i = Math.Max(0, from); i < to; i++)
        {
            var t = (double)i / rate - start;
            // Raised-cosine in and out, overlapping the next chord by the crossfade.
            var envelope = t < Crossfade ? Ease(t / Crossfade)
                : t > ChordSeconds ? Ease(1 - (t - ChordSeconds) / Crossfade)
                : 1;
            var phase = 2 * Math.PI * frequency * t;
            var tone = harmonics
                // A slightly detuned twin gives a slow chorus; the soft octave adds warmth.
                ? 0.6 * Math.Sin(phase) + 0.25 * Math.Sin(phase * 1.003) + 0.15 * Math.Sin(2 * phase)
                : 0.9 * Math.Sin(phase) + 0.1 * Math.Sin(2 * phase);
            buffer[i] += amplitude * envelope * tone;
        }
    }

    private static void AddPluck(double[] buffer, double start, double frequency, double amplitude)
    {
        var rate = Timeline.SampleRate;
        var from = (int)(start * rate);
        var to = Math.Min(buffer.Length, from + (int)(1.5 * rate));
        for (var i = Math.Max(0, from); i < to; i++)
        {
            var t = (double)i / rate - start;
            var attack = Math.Min(1, t / 0.005);
            var phase = 2 * Math.PI * frequency * t;
            buffer[i] += amplitude * attack * (Math.Sin(phase) * Math.Exp(-t * 3.5) + 0.3 * Math.Sin(2 * phase) * Math.Exp(-t * 6));
        }
    }

    private static double Ease(double x) => 0.5 - 0.5 * Math.Cos(Math.PI * Math.Clamp(x, 0, 1));

    private static short[] ToPcm(double[] buffer, double fadeIn, double fadeOut, bool normalize = true)
    {
        var rate = Timeline.SampleRate;
        var peak = normalize ? buffer.Select(Math.Abs).DefaultIfEmpty(0).Max() : 0;
        var gain = normalize && peak > 0 ? 0.9 / peak : 1;
        var result = new short[buffer.Length];
        var seconds = (double)buffer.Length / rate;
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = (double)i / rate;
            var fade = Math.Min(fadeIn > 0 ? Ease(t / fadeIn) : 1, fadeOut > 0 ? Ease((seconds - t) / fadeOut) : 1);
            result[i] = (short)Math.Clamp(Math.Round(buffer[i] * gain * fade * 32767), short.MinValue, short.MaxValue);
        }

        return result;
    }
}
