using System.Diagnostics;

namespace PromoVideo;

/// <summary>Runs a command-line tool with an argument list (never a shell string), optionally
/// feeding stdin, and returns stdout as bytes.</summary>
internal static class Processes
{
    public static async Task<byte[]> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken,
        string? stdin = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException($"{executable} did not start.");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException($"'{executable}' is not installed or not on PATH.", exception);
        }

        using (process)
        {
            using var output = new MemoryStream();
            var copy = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            if (stdin is not null)
            {
                await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken);
                process.StandardInput.Close();
            }

            await copy;
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{Path.GetFileName(executable)} exited {process.ExitCode}: {(await errors).Trim()}");
            }

            return output.ToArray();
        }
    }

    /// <summary>Any audio file as 16-bit mono PCM samples at the timeline's rate.</summary>
    public static async Task<short[]> DecodeAudioAsync(string file, CancellationToken cancellationToken)
    {
        var bytes = await RunAsync("ffmpeg",
            ["-hide_banner", "-loglevel", "error", "-i", file, "-f", "s16le", "-ac", "1", "-ar", Timeline.SampleRate.ToString(), "-"],
            cancellationToken);
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }
}
