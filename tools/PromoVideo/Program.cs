using PromoVideo;
using PromoVideo.Voice;

// Scripted promo videos (README.md in this folder):
//
//   dotnet run --project tools/PromoVideo -- validate <scenario.json>
//   dotnet run --project tools/PromoVideo -- voice    <scenario.json> [voice overrides]
//   dotnet run --project tools/PromoVideo -- render   <scenario.json> [voice overrides] [--show-browser] [--burn-captions] [--keep-frames]
//   dotnet run --project tools/PromoVideo -- voices   [--language tr-TR]
//
// Voice overrides: --provider say|google|piper  --voice <name>  --model <piper .onnx>  --rate <0.5-2>
//
// Output lands in artifacts/promo-video/<scenario name>/ (git-ignored), synthesised narration is
// cached in artifacts/promo-video/.voice-cache/.

var arguments = args.ToList();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var root = FindRepositoryRoot();
var artifacts = Path.Combine(root, "artifacts", "promo-video");

try
{
    switch (arguments.FirstOrDefault())
    {
        case "validate":
        {
            var scenario = await LoadAsync(arguments, cancellation.Token);
            Console.WriteLine($"OK: '{scenario.Title}', {scenario.Scenes.Count} scenes, voice {scenario.Voice.Provider}.");
            return 0;
        }
        case "voice":
        {
            var scenario = await LoadAsync(arguments, cancellation.Token);
            var file = await RendererFor(scenario, arguments).PreviewVoiceAsync(cancellation.Token);
            Console.WriteLine($"Narration preview: {file}");
            return 0;
        }
        case "render":
        {
            var scenario = await LoadAsync(arguments, cancellation.Token);
            var options = new RenderOptions(
                ShowBrowser: arguments.Contains("--show-browser"),
                BurnCaptions: arguments.Contains("--burn-captions"),
                KeepFrames: arguments.Contains("--keep-frames"));
            var file = await RendererFor(scenario, arguments).RenderAsync(options, cancellation.Token);
            var folder = Path.GetDirectoryName(file)!;
            Console.WriteLine($"""

                Done: {file}
                  subtitles.srt  → YouTube Studio › Subtitles › Upload file (with timing)
                  chapters.txt   → paste into the video description
                  narration.wav  → the voice track alone
                Folder: {folder}
                """);
            if (scenario.Voice.Provider == "say")
            {
                Console.WriteLine("Note: the 'say' voice is for drafts — Apple's licence is personal, non-commercial. Publish with google or piper.");
            }

            return 0;
        }
        case "voices":
        {
            var language = Option(arguments, "--language") ?? "tr-TR";
            foreach (var voice in await GoogleVoice.ListAsync(language, cancellation.Token))
            {
                Console.WriteLine(voice);
            }

            return 0;
        }
        default:
            Console.Error.WriteLine("Usage: validate|voice|render <scenario.json> [options]  ·  voices [--language tr-TR]  (see README.md)");
            return 2;
    }
}
catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or TimeoutException
                                      or FileNotFoundException or System.Text.Json.JsonException or HttpRequestException)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    return 1;
}

Renderer RendererFor(Scenario scenario, List<string> argumentList)
{
    var name = Path.GetFileNameWithoutExtension(argumentList[1]);
    return new Renderer(scenario, Path.Combine(artifacts, name), Path.Combine(artifacts, ".voice-cache"));
}

static async Task<Scenario> LoadAsync(List<string> arguments, CancellationToken cancellationToken)
{
    if (arguments.Count < 2 || arguments[1].StartsWith("--", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Give the scenario file after the command.");
    }

    var scenario = await ScenarioFile.LoadAsync(arguments[1], cancellationToken);

    // Command-line voice choices win over the file, so one script can be drafted with 'say' and
    // published with another voice without editing it.
    var voice = scenario.Voice with
    {
        Provider = Option(arguments, "--provider") ?? scenario.Voice.Provider,
        Name = Option(arguments, "--voice") ?? (Option(arguments, "--provider") is null ? scenario.Voice.Name : null),
        Model = Option(arguments, "--model") ?? scenario.Voice.Model,
        Rate = Option(arguments, "--rate") is { } rate
            ? double.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)
            : scenario.Voice.Rate
    };
    scenario = scenario with { Voice = voice };

    var errors = ScenarioRules.Validate(scenario);
    if (errors.Count > 0)
    {
        throw new InvalidDataException("The scenario has problems:\n  - " + string.Join("\n  - ", errors));
    }

    return scenario;
}

static string? Option(List<string> arguments, string name)
{
    var index = arguments.IndexOf(name);
    return index >= 0 && index + 1 < arguments.Count ? arguments[index + 1] : null;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AfterApply.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? Directory.GetCurrentDirectory();
}
