using System.Diagnostics;

namespace CvScanCorpus;

/// <summary>Headless Chrome as the PDF engine: the same engine behind most online CV builders'
/// "download as PDF", so fonts are embedded and the content stream follows paint order the way
/// real exports do.
///
/// Prefers Playwright's chrome-headless-shell when it is installed: desktop Chrome writes the PDF in
/// a second and then stays alive for its updater, so every case would wait out the timeout.</summary>
internal sealed class Chrome(string executable, string profileDirectory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static Chrome Locate(string workDirectory)
    {
        var playwright = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Caches", "ms-playwright");
        var shell = Directory.Exists(playwright)
            ? Directory.EnumerateFiles(playwright, "chrome-headless-shell", SearchOption.AllDirectories)
                .OrderDescending(StringComparer.Ordinal).FirstOrDefault()
            : null;

        var executable = Environment.GetEnvironmentVariable("CHROME_PATH")
                         ?? shell
                         ?? "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"Chrome not found at {executable}; set CHROME_PATH.");
        }

        // A throwaway profile: a headless run sharing the person's own profile would collide with
        // the Chrome they have open.
        return new Chrome(executable, Path.Combine(workDirectory, ".chrome-profile"));
    }

    public Task PrintToPdfAsync(string htmlPath, string pdfPath, CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "--no-pdf-header-footer", $"--print-to-pdf={pdfPath}", new Uri(htmlPath).AbsoluteUri);

    public Task ScreenshotAsync(string htmlPath, string pngPath, CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "--window-size=794,1123", "--hide-scrollbars", $"--screenshot={pngPath}",
            new Uri(htmlPath).AbsoluteUri);

    private async Task RunAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
                 {
                     "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
                     $"--user-data-dir={profileDirectory}", "--virtual-time-budget=3000", "--window-size=794,1123"
                 }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Chrome did not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);

        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.StandardOutput.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Chrome did not finish within {Timeout.TotalSeconds}s: {string.Join(' ', arguments)}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Chrome exited {process.ExitCode}: {await stderr}");
        }
    }
}
