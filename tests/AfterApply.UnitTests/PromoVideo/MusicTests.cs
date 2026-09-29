using PromoVideo;
using Shouldly;

namespace AfterApply.UnitTests.PromoVideo;

public class MusicTests
{
    [Fact]
    public void The_Ambient_Bed_Is_Exactly_As_Long_As_Asked_Peaks_Below_Full_Scale_And_Fades_At_Both_Ends()
    {
        var bed = Music.Ambient(10);
        var rate = Timeline.SampleRate;

        bed.Length.ShouldBe(10 * rate);
        bed.Max(s => Math.Abs((int)s)).ShouldBeInRange(29000, 29500); // 0.9 of full scale
        bed[0].ShouldBe((short)0);
        bed[^1].ShouldBe((short)0);
        // Audible in the middle, quieter in the fades.
        Rms(bed, 5 * rate, rate).ShouldBeGreaterThan(Rms(bed, 0, rate / 4) * 3);
    }

    [Fact]
    public void The_Same_Length_Gives_The_Same_Track()
    {
        Music.Ambient(3).ShouldBe(Music.Ambient(3));
    }

    [Fact]
    public void A_Supplied_Track_Keeps_Its_Level_And_Gets_The_Same_Fades()
    {
        var rate = Timeline.SampleRate;
        var flat = Enumerable.Repeat((short)10000, 10 * rate).ToArray();

        var faded = Music.Faded(flat);

        faded[0].ShouldBe((short)0);
        ((int)faded[5 * rate]).ShouldBeInRange(9999, 10000);
        faded[^1].ShouldBe((short)0);
    }

    [Fact]
    public void Note_Frequencies_Follow_Equal_Temperament()
    {
        Music.Frequency(69).ShouldBe(440);
        Music.Frequency(60).ShouldBe(261.6256, tolerance: 1e-3);
    }

    [Fact]
    public void Music_Ducks_Under_The_Voice_And_Everything_Is_Normalised()
    {
        Timeline.AudioFilter(null).ShouldBe("[1:a]loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000[a]");

        var withMusic = Timeline.AudioFilter(0.18);
        withMusic.ShouldStartWith("[2:a]volume=0.18[m];[1:a]asplit=2[sc][vo];[m][sc]sidechaincompress=");
        withMusic.ShouldEndWith("amix=inputs=2:duration=first:normalize=0,loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000[a]");
    }

    private static double Rms(short[] samples, int from, int count) =>
        Math.Sqrt(samples.Skip(from).Take(count).Average(s => (double)s * s));
}
