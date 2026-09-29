using PromoVideo;
using Shouldly;

namespace AfterApply.UnitTests.PromoVideo;

public class TimelineTests
{
    [Fact]
    public void A_Scene_Lasts_Its_Voice_Plus_Pauses_Or_Its_Minimum()
    {
        var scene = new Scene { Id = "a", Narration = "x", LeadIn = 0.5, Tail = 1 };

        Timeline.SceneSeconds(scene, 4).ShouldBe(5.5);
        Timeline.SceneSeconds(scene with { MinSeconds = 8 }, 4).ShouldBe(8);
        Timeline.SceneSeconds(scene with { Narration = null, MinSeconds = 3 }, 0).ShouldBe(3);
    }

    [Fact]
    public void Narration_Is_Cut_Into_Sentences_And_Long_Ones_At_Word_Boundaries()
    {
        var pieces = Timeline.SplitNarration(
            "Başvurdun.  Peki sonra ne oldu?\nBu çok uzun cümle, iki satıra sığmayacak kadar uzun olduğu için kelime sınırından bölünmesi gereken bir cümledir.");

        pieces[0].ShouldBe("Başvurdun.");
        pieces[1].ShouldBe("Peki sonra ne oldu?");
        pieces.Count.ShouldBe(4);
        pieces.ShouldAllBe(p => p.Length <= Timeline.MaxCaptionLength);
        string.Join(' ', pieces.Skip(2)).ShouldBe(
            "Bu çok uzun cümle, iki satıra sığmayacak kadar uzun olduğu için kelime sınırından bölünmesi gereken bir cümledir.");
        Timeline.SplitNarration("  ").ShouldBeEmpty();
    }

    [Fact]
    public void Captions_Share_The_Voice_Time_By_Length_Starting_When_The_Voice_Starts()
    {
        var scene = new Scene { Id = "a", Narration = "Kısa. Bu cümle üç kat uzun." };
        var span = new SceneSpan(scene, Start: 10, End: 20, VoiceStart: 10.4, VoiceSeconds: 8);

        var captions = Timeline.Captions(span);

        captions.Count.ShouldBe(2);
        captions[0].Start.ShouldBe(10.4);
        captions[1].Start.ShouldBe(captions[0].End);
        captions[1].End.ShouldBe(18.4, tolerance: 1e-9);
        // "Kısa." is 5 of the 26 characters: its share of the 8 seconds.
        (captions[0].End - captions[0].Start).ShouldBe(8.0 * 5 / 26, tolerance: 1e-9);
    }

    [Fact]
    public void Srt_Is_Numbered_With_Comma_Milliseconds()
    {
        var srt = Timeline.ToSrt([new Caption(0.4021, 1.5, "Bir."), new Caption(3661.25, 3662, "İki.")]);

        srt.ShouldBe("1\n00:00:00,402 --> 00:00:01,500\nBir.\n\n2\n01:01:01,250 --> 01:01:02,000\nİki.\n\n");
    }

    [Fact]
    public void Chapters_Start_At_Zero_And_Warn_About_What_YouTube_Would_Reject()
    {
        var spans = new List<SceneSpan>
        {
            new(new Scene { Id = "a", Chapter = "Giriş" }, 0.3, 12, 0.7, 10),
            new(new Scene { Id = "b" }, 12, 20, 12.4, 6),
            new(new Scene { Id = "c", Chapter = "Panel" }, 20, 26, 20.4, 5)
        };

        var (text, warnings) = Timeline.Chapters(spans);

        text.ShouldBe("00:00 Giriş\n00:20 Panel");
        warnings.ShouldBe(
        [
            "YouTube shows chapters only when there are at least three.",
            "Chapter 'Panel' is shorter than the 10 seconds YouTube requires."
        ]);
        Timeline.ChapterTime(3725).ShouldBe("1:02:05");
    }

    [Fact]
    public void Clips_Land_At_Their_Offsets_And_Overlaps_Are_Clipped_Not_Wrapped()
    {
        var rate = Timeline.SampleRate;
        var mixed = Timeline.Mix(1, [(0.5, [1000, 2000]), (0.5, [short.MaxValue, 0])]);

        mixed.Length.ShouldBe(rate);
        mixed[rate / 2 - 1].ShouldBe((short)0);
        mixed[rate / 2].ShouldBe(short.MaxValue);
        mixed[rate / 2 + 1].ShouldBe((short)2000);
        Timeline.Seconds(mixed).ShouldBe(1);
    }

    [Fact]
    public void A_Clip_Past_The_End_Is_Cut_Off()
    {
        Timeline.Mix(0.0001, [(0, new short[100])]).Length.ShouldBe(5);
    }

    [Fact]
    public void The_Wav_Header_Describes_Mono_16_Bit_At_The_Timeline_Rate()
    {
        var wav = Timeline.ToWav([1, -1]);

        wav.Length.ShouldBe(48);
        System.Text.Encoding.ASCII.GetString(wav, 0, 4).ShouldBe("RIFF");
        BitConverter.ToInt32(wav, 24).ShouldBe(Timeline.SampleRate);
        BitConverter.ToInt16(wav, 22).ShouldBe((short)1);
        BitConverter.ToInt16(wav, 34).ShouldBe((short)16);
        BitConverter.ToInt32(wav, 40).ShouldBe(4);
    }

    [Fact]
    public void Each_Frame_Is_Shown_Until_The_Next_And_The_Last_Until_The_End()
    {
        var list = Timeline.FrameList([("f0.jpg", 0), ("f1.jpg", 0.5)], end: 2);

        list.ShouldBe("ffconcat version 1.0\nfile 'f0.jpg'\nduration 0.5\nfile 'f1.jpg'\nduration 1.5\nfile 'f1.jpg'\n");
    }
}
