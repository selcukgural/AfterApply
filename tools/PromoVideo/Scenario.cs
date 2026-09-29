using System.Text.Json;
using System.Text.Json.Serialization;

namespace PromoVideo;

/// <summary>One video: where to point the browser, which voice reads the narration, and the scenes
/// in order. Loaded from a JSON file (comments and trailing commas allowed); see README.md.</summary>
public sealed record Scenario
{
    public string Title { get; init; } = string.Empty;

    /// <summary>tr or en — picks the site's URL prefix and the voice's language.</summary>
    public string Language { get; init; } = "tr";

    public string BaseUrl { get; init; } = "http://localhost:3000";

    public ViewportSettings Viewport { get; init; } = new();

    public OutputSettings Output { get; init; } = new();

    /// <summary>light or dark — the site follows prefers-color-scheme.</summary>
    public string ColorScheme { get; init; } = "light";

    /// <summary>Sign in before recording starts, with PROMO_EMAIL / PROMO_PASSWORD. Only against a
    /// local stack: a recording of a real account would put real data in a public video.</summary>
    public bool SignIn { get; init; }

    public VoiceSettings Voice { get; init; } = new();

    /// <summary>CSS selectors hidden for the whole recording (dev overlays, the feedback button...).</summary>
    public List<string> Hide { get; init; } = [];

    /// <summary>How the voice should say a word the subtitles spell differently, e.g.
    /// "ekariyerim.com" → "e kariyerim nokta kom". Applied to the voice only, never the subtitles.</summary>
    public Dictionary<string, string> Pronounce { get; init; } = [];

    /// <summary>Background music under the narration; null for none.</summary>
    public MusicSettings? Music { get; init; }

    public List<Scene> Scenes { get; init; } = [];
}

public sealed record ViewportSettings
{
    public int Width { get; init; } = 1920;

    public int Height { get; init; } = 1080;

    public double DeviceScaleFactor { get; init; } = 1;

    /// <summary>Phone emulation — for a vertical Shorts video.</summary>
    public bool Mobile { get; init; }
}

public sealed record OutputSettings
{
    public int Width { get; init; } = 1920;

    public int Height { get; init; } = 1080;

    public int Fps { get; init; } = 30;
}

public sealed record VoiceSettings
{
    /// <summary>say (macOS, drafts), google (Cloud Text-to-Speech), elevenlabs or piper (offline, open source).</summary>
    public string Provider { get; init; } = "say";

    /// <summary>Voice name in the provider's own terms; null takes the provider's default for the language.</summary>
    public string? Name { get; init; }

    /// <summary>1.0 is the voice's normal speed.</summary>
    public double Rate { get; init; } = 1.0;

    /// <summary>piper: path to the .onnx voice model. elevenlabs: the model id (default eleven_multilingual_v2).
    /// google: a Gemini-TTS model (e.g. gemini-2.5-pro-tts), with a Gemini voice name such as "Charon".</summary>
    public string? Model { get; init; }

    /// <summary>google with a Gemini-TTS model only: how to read, in plain words ("calm, warm product tour").</summary>
    public string? Prompt { get; init; }
}

public sealed record MusicSettings
{
    /// <summary>"ambient": a calm pad the tool composes itself — ours, so no licence question.</summary>
    public string? Generate { get; init; }

    /// <summary>A track of your own instead (e.g. from the YouTube Audio Library); looped or cut
    /// to the video's length.</summary>
    public string? File { get; init; }

    /// <summary>Music level relative to full scale before ducking; 0.15–0.25 sits well under a voice.</summary>
    public double Volume { get; init; } = 0.2;
}

public sealed record Scene
{
    public string Id { get; init; } = string.Empty;

    /// <summary>A YouTube chapter title starting at this scene; null continues the previous chapter.</summary>
    public string? Chapter { get; init; }

    /// <summary>What the voice says while the steps run. Null or empty: a silent scene.</summary>
    public string? Narration { get; init; }

    /// <summary>The scene lasts at least this long even when the narration is shorter.</summary>
    public double? MinSeconds { get; init; }

    /// <summary>Seconds between the scene starting and the voice starting.</summary>
    public double LeadIn { get; init; } = 0.4;

    /// <summary>Seconds held after the voice ends, before the next scene.</summary>
    public double Tail { get; init; } = 0.8;

    public List<Step> Steps { get; init; } = [];
}

/// <summary>One browser action. Which fields apply depends on <see cref="Action"/>:
/// goto(path) · click(target) · hover(target) · type(target, text, delayMs) · press(key) ·
/// scroll(target | by) · wait(seconds) · waitFor(target, seconds) · card(title, text) · hideCard.
/// A target is a CSS selector, or <c>text=Label</c> for a button/link by its visible text.</summary>
public sealed record Step
{
    public string Action { get; init; } = string.Empty;

    public string? Path { get; init; }

    public string? Target { get; init; }

    public string? Text { get; init; }

    /// <summary>card only: the big line of a full-screen title card.</summary>
    public string? Title { get; init; }

    public string? Key { get; init; }

    public double? Seconds { get; init; }

    public int? By { get; init; }

    public int? DelayMs { get; init; }
}

public static class ScenarioFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static Scenario Parse(string json) =>
        JsonSerializer.Deserialize<Scenario>(json, Options) ?? throw new InvalidDataException("The scenario file is empty.");

    public static async Task<Scenario> LoadAsync(string path, CancellationToken cancellationToken) =>
        Parse(await File.ReadAllTextAsync(path, cancellationToken));
}

public static class ScenarioRules
{
    public static readonly IReadOnlySet<string> Actions =
        new HashSet<string>(StringComparer.Ordinal) { "goto", "click", "hover", "type", "press", "scroll", "wait", "waitFor", "card", "hideCard" };

    public static readonly IReadOnlySet<string> Keys =
        new HashSet<string>(StringComparer.Ordinal) { "Enter", "Escape", "Tab", "ArrowDown", "ArrowUp", "Backspace" };

    public static readonly IReadOnlySet<string> Providers =
        new HashSet<string>(StringComparer.Ordinal) { "say", "google", "elevenlabs", "piper" };

    /// <summary>Every problem in the scenario, empty when it can be rendered.</summary>
    public static IReadOnlyList<string> Validate(Scenario scenario)
    {
        var errors = new List<string>();

        if (scenario.Language is not ("tr" or "en"))
        {
            errors.Add("language must be tr or en.");
        }

        if (!Uri.TryCreate(scenario.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
        {
            errors.Add("baseUrl must be an absolute http(s) URL.");
        }
        else if (scenario.SignIn && !IsLocalHost(baseUri.Host))
        {
            errors.Add("signIn is only allowed against a local stack (localhost, 127.0.0.1, [::1], *.localhost, *.test): " +
                       "a signed-in recording of a real account would publish real data.");
        }

        if (scenario.ColorScheme is not ("light" or "dark"))
        {
            errors.Add("colorScheme must be light or dark.");
        }

        if (scenario.Viewport.Width is < 320 or > 3840 || scenario.Viewport.Height is < 320 or > 3840)
        {
            errors.Add("viewport width/height must be between 320 and 3840.");
        }

        if (scenario.Viewport.DeviceScaleFactor is < 1 or > 3)
        {
            errors.Add("viewport.deviceScaleFactor must be between 1 and 3.");
        }

        if (scenario.Output.Width is < 320 or > 3840 || scenario.Output.Height is < 320 or > 3840
                                                      || scenario.Output.Width % 2 != 0 || scenario.Output.Height % 2 != 0)
        {
            errors.Add("output width/height must be even numbers between 320 and 3840.");
        }

        if (scenario.Output.Fps is < 15 or > 60)
        {
            errors.Add("output.fps must be between 15 and 60.");
        }

        if (!Providers.Contains(scenario.Voice.Provider))
        {
            errors.Add($"voice.provider must be one of: {string.Join(", ", Providers)}.");
        }

        if (scenario.Voice.Provider == "google" && string.IsNullOrWhiteSpace(scenario.Voice.Name))
        {
            errors.Add("voice.name is required for google (list them with the 'voices' command).");
        }

        if (scenario.Voice.Prompt is not null && (scenario.Voice.Provider != "google" || scenario.Voice.Model is null))
        {
            errors.Add("voice.prompt only works with google and a Gemini-TTS voice.model.");
        }

        if (scenario.Voice.Provider == "elevenlabs" && string.IsNullOrWhiteSpace(scenario.Voice.Name))
        {
            errors.Add("voice.name (the ElevenLabs voice id) is required for elevenlabs.");
        }

        // ElevenLabs takes speed only between 0.7 and 1.2.
        if (scenario.Voice.Provider == "elevenlabs" && scenario.Voice.Rate is < 0.7 or > 1.2)
        {
            errors.Add("voice.rate must be between 0.7 and 1.2 for elevenlabs.");
        }

        if (scenario.Voice.Provider == "piper" && string.IsNullOrWhiteSpace(scenario.Voice.Model))
        {
            errors.Add("voice.model (path to the .onnx file) is required for piper.");
        }

        if (scenario.Voice.Rate is < 0.5 or > 2)
        {
            errors.Add("voice.rate must be between 0.5 and 2.");
        }

        if (scenario.Music is { } music)
        {
            if ((music.Generate is null) == (music.File is null))
            {
                errors.Add("music needs exactly one of generate or file.");
            }
            else if (music.Generate is not null && music.Generate != "ambient")
            {
                errors.Add("music.generate must be \"ambient\".");
            }

            if (music.Volume is <= 0 or > 1)
            {
                errors.Add("music.volume must be between 0 and 1.");
            }
        }

        if (scenario.Pronounce.Any(p => string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Value)))
        {
            errors.Add("pronounce entries need a word and how to say it.");
        }

        if (scenario.Scenes.Count == 0)
        {
            errors.Add("scenes must not be empty.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < scenario.Scenes.Count; i++)
        {
            var scene = scenario.Scenes[i];
            var where = $"scenes[{i}]";

            if (string.IsNullOrWhiteSpace(scene.Id))
            {
                errors.Add($"{where}: id is required.");
            }
            else if (!ids.Add(scene.Id))
            {
                errors.Add($"{where}: id '{scene.Id}' is used twice.");
            }

            if (string.IsNullOrWhiteSpace(scene.Narration) && scene.MinSeconds is null or <= 0)
            {
                errors.Add($"{where}: a scene without narration needs minSeconds.");
            }

            if (scene.LeadIn < 0 || scene.Tail < 0)
            {
                errors.Add($"{where}: leadIn and tail must not be negative.");
            }

            for (var j = 0; j < scene.Steps.Count; j++)
            {
                errors.AddRange(ValidateStep(scene.Steps[j]).Select(e => $"{where}.steps[{j}]: {e}"));
            }
        }

        return errors;
    }

    private static IEnumerable<string> ValidateStep(Step step)
    {
        if (!Actions.Contains(step.Action))
        {
            yield return $"unknown action '{step.Action}' (known: {string.Join(", ", Actions)}).";
            yield break;
        }

        switch (step.Action)
        {
            case "goto" when string.IsNullOrWhiteSpace(step.Path) || !step.Path.StartsWith('/') || step.Path.StartsWith("//"):
                // A path, never a URL: the video stays on the scenario's own site.
                yield return "goto needs a path starting with a single '/'.";
                break;
            case "click" or "hover" or "waitFor" when string.IsNullOrWhiteSpace(step.Target):
                yield return $"{step.Action} needs a target.";
                break;
            case "type" when string.IsNullOrWhiteSpace(step.Target) || step.Text is null:
                yield return "type needs a target and text.";
                break;
            case "press" when step.Key is null || !Keys.Contains(step.Key):
                yield return $"press needs a key, one of: {string.Join(", ", Keys)}.";
                break;
            case "scroll" when string.IsNullOrWhiteSpace(step.Target) == (step.By is null):
                yield return "scroll needs exactly one of target or by.";
                break;
            case "card" when string.IsNullOrWhiteSpace(step.Title):
                yield return "card needs a title.";
                break;
            case "wait" when step.Seconds is null or <= 0 or > 60:
                yield return "wait needs seconds between 0 and 60.";
                break;
        }
    }

    /// <summary>The text the voice reads: the narration with each <see cref="Scenario.Pronounce"/>
    /// word replaced — only as a whole word, so "CV" changes in "CV'ni" but not inside another
    /// word — and longest first, so "ekariyerim.com" wins over "ekariyerim".</summary>
    public static string Spoken(string narration, IReadOnlyDictionary<string, string> pronounce) =>
        pronounce.OrderByDescending(p => p.Key.Length).Aggregate(narration, (text, p) =>
            System.Text.RegularExpressions.Regex.Replace(text,
                @"(?<![\p{L}\p{N}])" + System.Text.RegularExpressions.Regex.Escape(p.Key) + @"(?![\p{L}\p{N}])",
                p.Value.Replace("$", "$$", StringComparison.Ordinal)));

    public static bool IsLocalHost(string host) =>
        host is "localhost" or "127.0.0.1" or "[::1]" or "::1"
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase);
}
