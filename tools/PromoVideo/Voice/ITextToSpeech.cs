namespace PromoVideo.Voice;

/// <summary>Turns one scene's narration into an audio file the timeline can decode.</summary>
internal interface ITextToSpeech
{
    /// <summary>Everything that changes the sound — part of the cache key.</summary>
    string Fingerprint { get; }

    Task SynthesizeAsync(string text, string outputFile, CancellationToken cancellationToken);
}

internal static class TextToSpeech
{
    public static ITextToSpeech Create(Scenario scenario) => scenario.Voice.Provider switch
    {
        "say" => new SayVoice(scenario.Voice.Name ?? (scenario.Language == "tr" ? "Yelda" : "Samantha"), scenario.Voice.Rate),
        "google" => new GoogleVoice(scenario.Voice.Name!, scenario.Language == "tr" ? "tr-TR" : "en-US", scenario.Voice.Rate,
            scenario.Voice.Model, scenario.Voice.Prompt),
        "piper" => new PiperVoice(scenario.Voice.Model!, scenario.Voice.Rate),
        "elevenlabs" => new ElevenLabsVoice(scenario.Voice.Name!, scenario.Voice.Model ?? ElevenLabsVoice.DefaultModel, scenario.Voice.Rate),
        _ => throw new InvalidOperationException($"Unknown voice provider '{scenario.Voice.Provider}'.")
    };
}
