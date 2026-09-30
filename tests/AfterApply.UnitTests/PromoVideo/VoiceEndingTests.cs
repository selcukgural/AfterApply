using PromoVideo;
using Shouldly;

namespace AfterApply.UnitTests.PromoVideo;

public class VoiceEndingTests
{
    private static short[] Tone(double seconds, short amplitude) =>
        Enumerable.Range(0, (int)(Timeline.SampleRate * seconds)).Select(i => (short)(i % 2 == 0 ? amplitude : -amplitude)).ToArray();

    [Fact]
    public void A_Clip_That_Stops_While_Still_Loud_Ends_Abruptly()
    {
        Timeline.EndsAbruptly(Tone(0.5, 16000)).ShouldBeTrue();
    }

    [Fact]
    public void A_Clip_That_Dies_Away_Does_Not()
    {
        var clip = Tone(0.5, 16000).Concat(new short[Timeline.SampleRate / 10]).ToArray();
        Timeline.EndsAbruptly(clip).ShouldBeFalse();
        Timeline.EndsAbruptly([]).ShouldBeFalse();
    }

    [Fact]
    public void Fading_Out_Silences_The_End_And_Leaves_The_Rest()
    {
        var clip = Tone(0.5, 16000);
        var faded = Timeline.FadeOut(clip);

        faded.Length.ShouldBe(clip.Length);
        faded[^1].ShouldBe((short)0);
        var tail = faded[^(Timeline.SampleRate * 40 / 1000)..].Select(sample => Math.Abs((int)sample)).ToArray();
        tail.Zip(tail.Skip(2)).ShouldAllBe(pair => pair.Second <= pair.First);
        faded[..^(Timeline.SampleRate * 40 / 1000)].ShouldBe(clip[..^(Timeline.SampleRate * 40 / 1000)]);
        clip[^1].ShouldNotBe((short)0);
    }
}
