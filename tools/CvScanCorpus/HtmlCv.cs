using System.Globalization;
using System.Text;

namespace CvScanCorpus;

internal enum Layout { Classic, Sidebar, Banner }

/// <summary>How the text reaches the file's content stream, independent of how the page looks.
/// <see cref="HeadingsFirst"/> is what a design tool that keeps every label in its own layer
/// produces; <see cref="Shuffle"/> is a document whose boxes were added in no particular order.</summary>
internal enum Scramble { None, HeadingsFirst, Shuffle }

internal enum DateStyle { Slash, Years, MonthName, ShortMonth, Iso, Duration }

internal sealed record HtmlOptions
{
    public Layout Layout { get; init; } = Layout.Classic;
    public bool SidebarLeft { get; init; }
    public double GutterPt { get; init; } = 24;
    public bool SidebarFirstInDom { get; init; }
    public Scramble Scramble { get; init; } = Scramble.None;
    public bool CreativeHeadings { get; init; }
    public DateStyle Dates { get; init; } = DateStyle.Slash;
    public string Font { get; init; } = "Helvetica";
    public bool ManyFonts { get; init; }
    public bool ContactAsImage { get; init; }
    public bool FlattenTurkish { get; init; }

    /// <summary>Experience and education only — the too-short case.</summary>
    public bool Minimal { get; init; }
    public int Seed { get; init; } = 1;
}

internal static class HtmlCv
{
    private static readonly string[] MonthsTr = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
    private static readonly string[] MonthsEn = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames[..12];
    private static readonly string[] ExtraFonts = ["Georgia", "Courier New", "Trebuchet MS", "Verdana", "Palatino", "Avenir Next"];

    private sealed record Section(string Heading, List<string> Blocks, bool Side);

    public static string Build(Persona p, HtmlOptions o)
    {
        var tr = p.Lang == "tr";
        string H(string standardTr, string standardEn, string creativeTr, string creativeEn) =>
            o.CreativeHeadings ? (tr ? creativeTr : creativeEn) : tr ? standardTr : standardEn;

        var contact = o.ContactAsImage
            ? $"<canvas class=\"contact-img\" width=\"420\" height=\"40\" data-lines=\"{E(p.Email)}|{E(p.Phone)}\"></canvas><div>{E(p.City)}</div>"
            : $"<div>{E(p.Email)}</div><div>{E(p.Phone)}</div><div>{E(p.City)}</div>";

        var experience = p.Jobs.Select(job =>
                $"<div class=\"job\"><div class=\"role\">{E(job.Title)}</div>" +
                $"<div class=\"meta\">{E(job.Company)} · {E(Dates(job, o.Dates, tr))}</div>" +
                $"<ul>{string.Concat(job.Bullets.Select(bullet => $"<li>{E(bullet)}</li>"))}</ul></div>")
            .ToList();

        var education = $"<div class=\"role\">{E(p.School)}</div><div class=\"meta\">{E(p.Degree)} · " +
                        (o.Dates == DateStyle.Duration
                            ? E(tr ? "4 yıl" : "4 years")
                            : $"{p.SchoolStart} - {p.SchoolEnd}") + "</div>";

        var sidebar = o.Layout == Layout.Sidebar;
        var sections = new List<Section>
        {
            new(H("HAKKIMDA", "ABOUT ME", "BEN KİMİM", "WHO I AM"), [$"<p>{E(p.Summary)}</p>"], Side: sidebar),
            new(H("İŞ DENEYİMİ", "WORK EXPERIENCE", "YOLCULUĞUM", "MY JOURNEY"), experience, Side: false),
            new(H("EĞİTİM", "EDUCATION", "OKUL YILLARIM", "WHERE I STUDIED"), [education], Side: false),
            new(H("YETENEKLER", "SKILLS", "ALET ÇANTAM", "MY TOOLBOX"),
                [sidebar
                    ? string.Concat(p.Skills.Select(skill => $"<div>{E(skill)}</div>"))
                    : $"<p>{E(string.Join(", ", p.Skills))}</p>"], Side: sidebar),
            new(H("DİLLER", "LANGUAGES", "KONUŞTUKLARIM", "WHAT I SPEAK"),
                [string.Concat(p.Languages.Select(language => $"<div>{E(language)}</div>"))], Side: sidebar),
            new(H("SERTİFİKALAR", "CERTIFICATES", "BELGELERİM", "PAPERS"),
                [string.Concat(p.Certificates.Select(certificate => $"<div>{E(certificate)}</div>"))], Side: sidebar)
        };

        if (o.Minimal)
        {
            sections = sections.Skip(1).Take(2).ToList();
        }

        var identity = $"<div data-b class=\"name\">{E(p.FullName)}</div><div data-b class=\"title\">{E(p.Title)}</div>";

        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><style>");
        html.Append(Css(o));
        html.Append("</style></head><body>");

        switch (o.Layout)
        {
            case Layout.Classic:
                html.Append("<div class=\"page\">").Append(identity)
                    .Append($"<div data-b class=\"contact-line\">{contact}</div>");
                AppendSections(html, sections, o);
                html.Append("</div>");
                break;

            case Layout.Banner:
                html.Append($"<div class=\"banner\"><div data-b class=\"contact-line top\">{contact}</div>{identity}</div>")
                    .Append("<div class=\"page\">");
                AppendSections(html, sections, o);
                html.Append("</div>");
                break;

            case Layout.Sidebar:
                var main = new StringBuilder("<div class=\"main\">").Append(identity);
                AppendSections(main, sections.Where(section => !section.Side).ToList(), o);
                main.Append("</div>");

                var side = new StringBuilder("<div class=\"side\">");
                side.Append($"<h2 data-b>{E(H("İLETİŞİM", "CONTACT", "BANA ULAŞ", "REACH ME"))}</h2><div data-b class=\"block\">{contact}</div>");
                AppendSections(side, sections.Where(section => section.Side).ToList(), o);
                side.Append("</div>");

                html.Append("<div class=\"page grid\">")
                    .Append(o.SidebarFirstInDom ? side : main)
                    .Append(o.SidebarFirstInDom ? main : side)
                    .Append("</div>");
                break;
        }

        html.Append("<script>").Append(Script(o)).Append("</script></body></html>");

        var text = html.ToString();
        return o.FlattenTurkish ? Personas.Ascii(text) : text;
    }

    private static void AppendSections(StringBuilder html, List<Section> sections, HtmlOptions o)
    {
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            var font = o.ManyFonts ? $" style=\"font-family:'{ExtraFonts[index % ExtraFonts.Length]}'\"" : string.Empty;
            html.Append($"<section{font}><h2 data-b>{E(section.Heading)}</h2>");
            foreach (var block in section.Blocks)
            {
                html.Append($"<div data-b class=\"block\">{block}</div>");
            }

            html.Append("</section>");
        }
    }

    public static string Dates(Job job, DateStyle style, bool tr)
    {
        var present = tr ? "Halen" : "Present";
        var months = tr ? MonthsTr : MonthsEn;

        return style switch
        {
            DateStyle.Slash => $"{job.StartMonth:00}/{job.StartYear} – {(job.EndYear is { } y ? $"{job.EndMonth:00}/{y}" : present)}",
            DateStyle.Years => $"{job.StartYear} – {(job.EndYear?.ToString(CultureInfo.InvariantCulture) ?? present)}",
            DateStyle.MonthName => $"{months[job.StartMonth - 1]} {job.StartYear} – {(job.EndYear is { } y ? $"{months[job.EndMonth!.Value - 1]} {y}" : present)}",
            DateStyle.ShortMonth => $"{months[job.StartMonth - 1][..3]} {job.StartYear} – {(job.EndYear is { } y ? $"{months[job.EndMonth!.Value - 1][..3]} {y}" : present)}",
            DateStyle.Iso => $"{job.StartYear}-{job.StartMonth:00}/{(job.EndYear is { } y ? $"{y}-{job.EndMonth:00}" : " " + present)}",
            DateStyle.Duration => tr
                ? $"{(job.EndYear ?? 2026) - job.StartYear} yıl"
                : $"{(job.EndYear ?? 2026) - job.StartYear} years",
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
    }

    private static string Css(HtmlOptions o)
    {
        var sideWidth = "62mm";
        var columns = o.SidebarLeft
            ? $"{sideWidth} {o.GutterPt.ToString(CultureInfo.InvariantCulture)}pt 1fr"
            : $"1fr {o.GutterPt.ToString(CultureInfo.InvariantCulture)}pt {sideWidth}";
        var mainColumn = o.SidebarLeft ? 3 : 1;
        var sideColumn = o.SidebarLeft ? 1 : 3;

        return $$"""
            @page { size: A4; margin: 0; }
            html, body { margin: 0; padding: 0; }
            body { font-family: '{{o.Font}}', sans-serif; font-size: 9.5pt; color: #222; line-height: 1.35; }
            .page { width: 210mm; box-sizing: border-box; padding: 16mm 16mm 12mm; }
            .grid { display: grid; grid-template-columns: {{columns}}; }
            .main { grid-column: {{mainColumn}}; grid-row: 1; text-align: justify; }
            .side { grid-column: {{sideColumn}}; grid-row: 1; }
            .name { font-size: 22pt; font-weight: bold; }
            .title { font-size: 12pt; margin-bottom: 6pt; }
            .contact-line div { display: inline; margin-right: 12pt; }
            .side .block div, .side .contact-line div { display: block; margin: 0; }
            h2 { font-size: 11pt; margin: 12pt 0 4pt; letter-spacing: 0.5pt; }
            ul { margin: 2pt 0 6pt; padding-left: 12pt; }
            .role { font-weight: bold; }
            .meta { font-size: 9pt; color: #555; }
            p { margin: 0 0 4pt; }
            .banner { background: #1f3a5f; color: #fff; width: 210mm; box-sizing: border-box; padding: 5mm 16mm 6mm; }
            .banner .contact-line { font-size: 8.5pt; }
            .banner .meta { color: #ddd; }
            canvas.contact-img { display: block; width: 105mm; height: 10mm; }
            """;
    }

    /// <summary>
    /// Runs before Chrome prints. Draws the image-only contact block, and for a scrambled case
    /// pins every block to where the normal layout put it and then reorders the DOM, so the page
    /// looks identical while the content stream — which follows DOM paint order — does not.
    /// </summary>
    private static string Script(HtmlOptions o) => $$"""
        (function () {
          document.querySelectorAll('canvas.contact-img').forEach(function (c) {
            var ctx = c.getContext('2d');
            ctx.font = '15px Helvetica';
            ctx.fillStyle = '#222';
            c.getAttribute('data-lines').split('|').forEach(function (line, i) { ctx.fillText(line, 0, 16 + i * 20); });
          });

          var mode = '{{o.Scramble}}';
          if (mode === 'None') return;

          var props = ['fontSize', 'fontWeight', 'fontFamily', 'fontStyle', 'color', 'lineHeight', 'textTransform',
            'letterSpacing', 'textAlign', 'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft'];
          var height = document.documentElement.scrollHeight;
          var blocks = [].slice.call(document.querySelectorAll('[data-b]')).map(function (b) {
            var r = b.getBoundingClientRect(), cs = getComputedStyle(b), st = {};
            props.forEach(function (p) { st[p] = cs[p]; });
            return { b: b, left: r.left + scrollX, top: r.top + scrollY, width: r.width, st: st };
          });

          blocks.forEach(function (i) {
            var s = i.b.style;
            s.position = 'absolute'; s.left = i.left + 'px'; s.top = i.top + 'px'; s.width = i.width + 'px';
            s.margin = '0'; s.boxSizing = 'border-box';
            for (var k in i.st) s[k] = i.st[k];
          });

          var order;
          if (mode === 'HeadingsFirst') {
            order = blocks.filter(function (i) { return i.b.tagName === 'H2'; })
              .concat(blocks.filter(function (i) { return i.b.tagName !== 'H2'; }));
          } else {
            var seed = {{o.Seed}};
            var rnd = function () { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed / 0x7fffffff; };
            order = blocks.slice();
            for (var n = order.length - 1; n > 0; n--) {
              var j = Math.floor(rnd() * (n + 1)); var t = order[n]; order[n] = order[j]; order[j] = t;
            }
          }

          document.body.style.position = 'relative';
          document.body.style.minHeight = height + 'px';
          order.forEach(function (i) { document.body.appendChild(i.b); });
        })();
        """;

    /// <summary>Markup characters only. HtmlEncode would also turn ö, ü and ç into numeric
    /// entities, which the flattened-Turkish cases could then no longer flatten.</summary>
    private static string E(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
