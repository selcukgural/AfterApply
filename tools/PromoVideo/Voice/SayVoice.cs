using System.Globalization;

namespace PromoVideo.Voice;

/// <summary>
/// macOS's built-in speech (<c>say</c>), with the system's Turkish voice "Yelda" by default.
/// Offline and instant, which makes it the right voice for drafting a script and timing scenes —
/// but not for publishing: Apple's licence limits the system voices to personal, non-commercial
/// content. Render the final video with google or piper.
/// </summary>
internal sealed class SayVoice(string voice, double rate) : ITextToSpeech
{
    // say's own default speaking rate, in words per minute.
    private const int BaseWordsPerMinute = 175;

    public string Fingerprint => $"say|{voice}|{rate.ToString(CultureInfo.InvariantCulture)}";

    public async Task SynthesizeAsync(string text, string outputFile, CancellationToken cancellationToken)
    {
        var textFile = outputFile + ".txt";
        await File.WriteAllTextAsync(textFile, text, cancellationToken);
        try
        {
            var wordsPerMinute = ((int)Math.Round(BaseWordsPerMinute * rate)).ToString(CultureInfo.InvariantCulture);
            await Processes.RunAsync("say", ["-v", voice, "-r", wordsPerMinute, "-o", outputFile, "-f", textFile], cancellationToken);
        }
        finally
        {
            File.Delete(textFile);
        }
    }
}
