using System.Text.Json;

namespace CvScanCorpus;

internal static class Generator
{
    public static async Task RunAsync(string outDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outDirectory);
        var htmlDirectory = Directory.CreateDirectory(Path.Combine(outDirectory, "html")).FullName;
        var chrome = Chrome.Locate(outDirectory);
        var cases = CorpusPlan.Build();

        foreach (var item in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var persona = Personas.Create(new Random(item.PersonaSeed), item.Lang, item.Jobs, item.Bullets);
            var target = Path.Combine(Path.GetFullPath(outDirectory), item.FileName);

            if (item.Docx is { } variant)
            {
                DocxCv.Write(target, persona, variant);
            }
            else
            {
                var options = item.Html! with { Minimal = item.Minimal };
                var htmlPath = Path.Combine(htmlDirectory, $"{item.Id:000}.html");
                await File.WriteAllTextAsync(htmlPath, HtmlCv.Build(persona, options), cancellationToken);

                if (item.ImageOnly)
                {
                    // Screenshot the page, then print a page that is nothing but that picture: what
                    // "export as image" or a phone scan hands a parser.
                    var pngPath = Path.Combine(htmlDirectory, $"{item.Id:000}.png");
                    await chrome.ScreenshotAsync(htmlPath, pngPath, cancellationToken);
                    htmlPath = Path.Combine(htmlDirectory, $"{item.Id:000}-image.html");
                    await File.WriteAllTextAsync(htmlPath,
                        "<!doctype html><html><head><style>@page{size:A4;margin:0}body{margin:0}img{width:210mm;height:297mm;display:block}</style></head>" +
                        $"<body><img src=\"{Path.GetFileName(pngPath)}\"></body></html>", cancellationToken);
                }

                await chrome.PrintToPdfAsync(htmlPath, target, cancellationToken);
            }

            Console.WriteLine($"  {item.FileName}");
        }

        await File.WriteAllTextAsync(Path.Combine(outDirectory, "manifest.json"),
            JsonSerializer.Serialize(cases, Json.Options), cancellationToken);

        Console.WriteLine($"{cases.Count} cases written to {Path.GetFullPath(outDirectory)}");
    }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
