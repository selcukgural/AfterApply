using System.Globalization;
using PromoVideo.Voice;

namespace PromoVideo;

public sealed record RenderOptions(bool ShowBrowser, bool BurnCaptions, bool KeepFrames);

/// <summary>Narration first (so every scene knows how long it must last), then one continuous
/// recording scene by scene, then ffmpeg: frames → video, clips → narration track, + subtitles.</summary>
internal sealed class Renderer(Scenario scenario, string scenarioDirectory, string outputDirectory, string cacheDirectory)
{
    /// <summary>Every upload step's file, checked before anything is paid for or recorded.</summary>
    private void EnsureUploadFilesExist()
    {
        foreach (var step in scenario.Scenes.SelectMany(scene => scene.Steps).Where(step => step.Action == "upload"))
        {
            var file = ScenarioRules.ResolveFile(scenarioDirectory, step.File!);
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"Upload file not found: {file}");
            }
        }
    }

    public async Task<IReadOnlyList<short[]>> SynthesizeAsync(CancellationToken cancellationToken)
    {
        var cache = new NarrationCache(cacheDirectory, TextToSpeech.Create(scenario));
        var clips = new List<short[]>(scenario.Scenes.Count);
        foreach (var scene in scenario.Scenes)
        {
            clips.Add(string.IsNullOrWhiteSpace(scene.Narration)
                ? []
                : await cache.GetAsync(ScenarioRules.Spoken(scene.Narration.Trim(), scenario.Pronounce), cancellationToken));
            Console.WriteLine($"  voice  {scene.Id,-24} {Timeline.Seconds(clips[^1]),6:0.0} s");
        }

        return clips;
    }

    /// <summary>The narration alone, scene after scene with its pauses — for listening to a script
    /// and its pacing before recording anything.</summary>
    public async Task<string> PreviewVoiceAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var clips = await SynthesizeAsync(cancellationToken);
        var placed = new List<(double, short[])>();
        var at = 0.0;
        for (var i = 0; i < clips.Count; i++)
        {
            placed.Add((at + scenario.Scenes[i].LeadIn, clips[i]));
            at += Timeline.SceneSeconds(scenario.Scenes[i], Timeline.Seconds(clips[i]));
        }

        var file = Path.Combine(outputDirectory, "narration-preview.wav");
        await File.WriteAllBytesAsync(file, Timeline.ToWav(Timeline.Mix(at, placed)), cancellationToken);
        Console.WriteLine($"  total  {at,6:0.0} s (without the time the steps themselves need)");
        return file;
    }

    public async Task<string> RenderAsync(RenderOptions options, CancellationToken cancellationToken)
    {
        EnsureUploadFilesExist();
        Directory.CreateDirectory(outputDirectory);
        var clips = await SynthesizeAsync(cancellationToken);

        var (email, password) = scenario.SignIn ? ReadCredentials() : (null, null);
        var frames = Path.Combine(outputDirectory, "frames");
        if (Directory.Exists(frames))
        {
            Directory.Delete(frames, recursive: true);
        }

        var spans = new List<SceneSpan>();
        double recordingEnd;
        double zero;
        List<(string File, double At)> frameList;

        await using (var browser = await Browser.LaunchAsync(scenario, options.ShowBrowser, cancellationToken))
        {
            if (email is not null)
            {
                Console.WriteLine("  sign in (not recorded)");
                await browser.SignInAsync(email, password!, cancellationToken);
            }

            // The first scene usually starts with a goto; landing somewhere neutral first keeps the
            // login page's last frame out of the video.
            await browser.GotoAsync("/" + scenario.Language, cancellationToken);
            await browser.StartRecordingAsync(frames, cancellationToken);
            zero = browser.FirstFrameAt;

            for (var i = 0; i < scenario.Scenes.Count; i++)
            {
                var scene = scenario.Scenes[i];
                var voiceSeconds = Timeline.Seconds(clips[i]);
                var start = browser.Now;
                var span = new SceneSpan(scene, start - zero, 0, start - zero + scene.LeadIn, voiceSeconds);

                using var captionStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var captions = options.BurnCaptions
                    ? ShowCaptionsAsync(browser, Timeline.Captions(span), zero, captionStop.Token)
                    : Task.CompletedTask;

                for (var j = 0; j < scene.Steps.Count; j++)
                {
                    try
                    {
                        await RunStepAsync(browser, scene.Steps[j], cancellationToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        var shot = Path.Combine(outputDirectory, "failed-step.png");
                        await browser.ScreenshotAsync(shot, CancellationToken.None);
                        throw new InvalidOperationException(
                            $"Scene '{scene.Id}', step {j} ({scene.Steps[j].Action}) failed: {exception.Message}\n  Page at failure: {shot}", exception);
                    }
                }

                var remaining = start + Timeline.SceneSeconds(scene, voiceSeconds) - browser.Now;
                if (remaining > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(remaining), cancellationToken);
                }

                await captionStop.CancelAsync();
                await captions;
                if (options.BurnCaptions)
                {
                    await browser.ShowCaptionAsync(null, cancellationToken);
                }

                spans.Add(span with { End = browser.Now - zero });
                Console.WriteLine($"  scene  {scene.Id,-24} {spans[^1].End - spans[^1].Start,6:0.0} s");
            }

            recordingEnd = browser.Now - zero;
            await browser.StopRecordingAsync(cancellationToken);
            frameList = browser.Frames.Select(f => (f.File, f.At - zero)).ToList();
        }

        return await AssembleAsync(spans, clips, frameList, recordingEnd, frames, options, cancellationToken);
    }

    private static async Task ShowCaptionsAsync(Browser browser, IReadOnlyList<Caption> captions, double zero,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var caption in captions)
            {
                var wait = caption.Start - (browser.Now - zero);
                if (wait > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(wait), cancellationToken);
                }

                await browser.ShowCaptionAsync(caption.Text, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The scene ended.
        }
    }

    private Task RunStepAsync(Browser browser, Step step, CancellationToken cancellationToken) => step.Action switch
    {
        "goto" => browser.GotoAsync(step.Path!, cancellationToken),
        "click" => browser.ClickAsync(step.Target!, cancellationToken),
        "hover" => browser.HoverAsync(step.Target!, cancellationToken),
        "type" => browser.TypeAsync(step.Target!, step.Text!, step.DelayMs ?? Browser.ReadableTypingDelayMs, step.Clear, cancellationToken),
        "press" => browser.PressAsync(step.Key!, cancellationToken),
        "scroll" => browser.ScrollAsync(step.Target, step.By, cancellationToken),
        "wait" => Task.Delay(TimeSpan.FromSeconds(step.Seconds!.Value), cancellationToken),
        "waitFor" => browser.WaitForAsync(step.Target!, step.Seconds ?? 10, cancellationToken),
        "drag" => browser.DragAsync(step.Target!, step.To!, cancellationToken),
        "upload" => browser.UploadAsync(step.Target!, ScenarioRules.ResolveFile(scenarioDirectory, step.File!), cancellationToken),
        "card" => browser.ShowCardAsync(step.Title!, step.Text, cancellationToken),
        "hideCard" => browser.HideCardAsync(cancellationToken),
        _ => throw new InvalidOperationException($"Unknown action '{step.Action}'.")
    };

    private async Task<string> AssembleAsync(List<SceneSpan> spans, IReadOnlyList<short[]> clips, List<(string File, double At)> frames,
        double end, string frameDirectory, RenderOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  encode {frames.Count} frames, {end:0.0} s");
        var output = scenario.Output;

        await File.WriteAllTextAsync(Path.Combine(frameDirectory, "frames.txt"), Timeline.FrameList(frames, end), cancellationToken);
        var video = Path.Combine(outputDirectory, "video-only.mp4");
        await Processes.RunAsync("ffmpeg",
        [
            "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", Path.Combine(frameDirectory, "frames.txt"),
            .. Timeline.VideoEncodeArguments(output),
            // The concat list repeats its last frame (see FrameList), which some ffmpeg builds count
            // for a second duration; the recording's own length is the video's length.
            "-t", end.ToString("0.###", CultureInfo.InvariantCulture), video
        ], cancellationToken);

        var narration = Path.Combine(outputDirectory, "narration.wav");
        var placed = spans.Select((span, i) => (span.VoiceStart, clips[i]));
        await File.WriteAllBytesAsync(narration, Timeline.ToWav(Timeline.Mix(end, placed)), cancellationToken);

        var captions = spans.SelectMany(Timeline.Captions).ToList();
        var subtitles = Path.Combine(outputDirectory, "subtitles.srt");
        await File.WriteAllTextAsync(subtitles, Timeline.ToSrt(captions), cancellationToken);

        var (chapterText, chapterWarnings) = Timeline.Chapters(spans);
        var chapters = Path.Combine(outputDirectory, "chapters.txt");
        await File.WriteAllTextAsync(chapters, chapterText, cancellationToken);

        var final = Path.Combine(outputDirectory, "video.mp4");
        var arguments = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", video, "-i", narration };
        var music = await PrepareMusicAsync(end, cancellationToken);
        if (music is not null)
        {
            arguments.AddRange(["-i", music]);
        }

        var subtitleInput = music is null ? 2 : 3;
        if (captions.Count > 0 && !options.BurnCaptions)
        {
            arguments.AddRange(["-i", subtitles]);
        }

        arguments.AddRange(["-filter_complex", Timeline.AudioFilter(music is null ? null : scenario.Music!.Volume), "-map", "0:v", "-map", "[a]"]);
        if (captions.Count > 0 && !options.BurnCaptions)
        {
            // A soft subtitle track players can switch on; YouTube ignores it and takes subtitles.srt.
            arguments.AddRange(["-map", $"{subtitleInput}:s", "-c:s", "mov_text",
                "-metadata:s:s:0", "language=" + (scenario.Language == "tr" ? "tur" : "eng")]);
        }

        arguments.AddRange(["-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", final]);
        await Processes.RunAsync("ffmpeg", arguments, cancellationToken);

        File.Delete(video);
        if (!options.KeepFrames)
        {
            Directory.Delete(frameDirectory, recursive: true);
        }

        foreach (var warning in chapterWarnings)
        {
            Console.WriteLine($"  note   {warning}");
        }

        return final;
    }

    /// <summary>The music bed as a WAV of exactly the video's length, or null without music.</summary>
    private async Task<string?> PrepareMusicAsync(double seconds, CancellationToken cancellationToken)
    {
        if (scenario.Music is not { } music)
        {
            return null;
        }

        var file = Path.Combine(outputDirectory, "music.wav");
        if (music.Generate is not null)
        {
            var raw = Path.Combine(outputDirectory, "music-dry.wav");
            await File.WriteAllBytesAsync(raw, Timeline.ToWav(Music.Ambient(seconds)), cancellationToken);
            // A gentle low-pass takes the edge off the sine harmonics; two short echoes give it a room.
            await Processes.RunAsync("ffmpeg",
            [
                "-hide_banner", "-loglevel", "error", "-y", "-i", raw,
                "-af", "lowpass=f=3800,aecho=0.8:0.6:90|170:0.25|0.15", "-t", seconds.ToString("0.###", CultureInfo.InvariantCulture), file
            ], cancellationToken);
            File.Delete(raw);
            return file;
        }

        var source = Path.GetFullPath(music.File!);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"music.file not found: {source}");
        }

        var looped = Path.Combine(outputDirectory, "music-looped.wav");
        await Processes.RunAsync("ffmpeg",
        [
            "-hide_banner", "-loglevel", "error", "-y", "-stream_loop", "-1", "-i", source,
            "-t", seconds.ToString("0.###", CultureInfo.InvariantCulture), "-ac", "1", "-ar", Timeline.SampleRate.ToString(CultureInfo.InvariantCulture), looped
        ], cancellationToken);
        var samples = await Processes.DecodeAudioAsync(looped, cancellationToken);
        File.Delete(looped);
        await File.WriteAllBytesAsync(file, Timeline.ToWav(Music.Faded(samples)), cancellationToken);
        return file;
    }

    private static (string Email, string Password) ReadCredentials()
    {
        var email = Environment.GetEnvironmentVariable("PROMO_EMAIL");
        var password = Environment.GetEnvironmentVariable("PROMO_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("signIn is on: set PROMO_EMAIL and PROMO_PASSWORD to the local demo account.");
        }

        return (email, password);
    }
}
