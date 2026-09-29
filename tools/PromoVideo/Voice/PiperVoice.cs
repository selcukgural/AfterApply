using System.Globalization;

namespace PromoVideo.Voice;

/// <summary>
/// Piper (github.com/rhasspy/piper): open-source neural speech that runs offline. Needs the
/// <c>piper</c> command and a voice model (.onnx + .onnx.json). Each voice has its own licence on
/// its model card — check it before publishing.
/// </summary>
internal sealed class PiperVoice(string model, double rate) : ITextToSpeech
{
    public string Fingerprint => $"piper|{Path.GetFullPath(model)}|{rate.ToString(CultureInfo.InvariantCulture)}";

    public Task SynthesizeAsync(string text, string outputFile, CancellationToken cancellationToken) =>
        // length_scale is the inverse of speed: 0.8 speaks faster, 1.25 slower.
        Processes.RunAsync("piper",
            ["--model", model, "--output_file", outputFile, "--length_scale", (1 / rate).ToString("0.###", CultureInfo.InvariantCulture)],
            cancellationToken, stdin: text);
}
