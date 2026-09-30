using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace PromoVideo;

/// <summary>
/// A throwaway Chrome driven over CDP: one page, a visible fake cursor (a headless browser has
/// none, and a demo video without one is hard to follow), real mouse and keyboard input so React
/// handlers fire, and a screencast that writes every painted frame to disk.
/// </summary>
internal sealed class Browser : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _profile;
    private readonly Cdp _cdp;
    private readonly string _session;
    private readonly Scenario _scenario;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private (double X, double Y) _cursor;

    private Channel<(string Data, double At)>? _frames;
    private Task? _frameWriter;
    private readonly List<(string File, double At)> _written = [];

    private Browser(Process process, string profile, Cdp cdp, string session, Scenario scenario)
    {
        _process = process;
        _profile = profile;
        _cdp = cdp;
        _session = session;
        _scenario = scenario;
        _cursor = (scenario.Viewport.Width / 2.0, scenario.Viewport.Height / 2.0);
    }

    /// <summary>Seconds since the browser started; the one clock frames and scene marks share.</summary>
    public double Now => _clock.Elapsed.TotalSeconds;

    public IReadOnlyList<(string File, double At)> Frames => _written;

    /// <summary>When the first screencast frame arrived — the video's zero.</summary>
    public double FirstFrameAt { get; private set; } = double.NaN;

    public static async Task<Browser> LaunchAsync(Scenario scenario, bool visible, CancellationToken cancellationToken)
    {
        var executable = Environment.GetEnvironmentVariable("CHROME_PATH")
                         ?? "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"Chrome not found at {executable}; set CHROME_PATH.");
        }

        // A fresh profile every run: nothing from the person's own browser (sessions, extensions,
        // autofill) can end up on screen.
        var profile = Path.Combine(Path.GetTempPath(), "promo-video-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);

        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in new[]
                 {
                     "--remote-debugging-port=0", $"--user-data-dir={profile}", "--no-first-run",
                     "--no-default-browser-check", "--disable-extensions", "--hide-scrollbars", "--mute-audio",
                     "--force-color-profile=srgb", "--disable-features=Translate,AutofillServerCommunication",
                     "--password-store=basic", $"--window-size={scenario.Viewport.Width},{scenario.Viewport.Height}",
                     "about:blank"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        if (!visible)
        {
            start.ArgumentList.Insert(0, "--headless=new");
        }

        var process = Process.Start(start) ?? throw new InvalidOperationException("Chrome did not start.");
        // Chrome is chatty on stderr; drain it so its pipe never fills and blocks the browser.
        _ = process.StandardError.ReadToEndAsync(CancellationToken.None);

        var endpoint = await ReadDevToolsEndpointAsync(profile, cancellationToken);
        var cdp = await Cdp.ConnectAsync(endpoint, cancellationToken);

        var target = await cdp.SendAsync("Target.createTarget", new { url = "about:blank" }, cancellationToken: cancellationToken);
        var attached = await cdp.SendAsync("Target.attachToTarget",
            new { targetId = target.GetProperty("targetId").GetString(), flatten = true }, cancellationToken: cancellationToken);
        var session = attached.GetProperty("sessionId").GetString()!;

        var browser = new Browser(process, profile, cdp, session, scenario);
        await browser.SetUpPageAsync(cancellationToken);
        return browser;
    }

    private static async Task<Uri> ReadDevToolsEndpointAsync(string profile, CancellationToken cancellationToken)
    {
        // With --remote-debugging-port=0 Chrome picks a free port and writes it, with the browser's
        // WebSocket path, into this file.
        var file = Path.Combine(profile, "DevToolsActivePort");
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(file))
            {
                var lines = (await File.ReadAllLinesAsync(file, cancellationToken)).Where(l => l.Length > 0).ToArray();
                if (lines.Length >= 2)
                {
                    return new Uri($"ws://127.0.0.1:{lines[0]}{lines[1]}");
                }
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("Chrome did not open its debugging port within 20 seconds.");
    }

    private async Task SetUpPageAsync(CancellationToken cancellationToken)
    {
        await SendAsync("Page.enable", null, cancellationToken);
        await SendAsync("Runtime.enable", null, cancellationToken);
        await SendAsync("Emulation.setDeviceMetricsOverride", new
        {
            width = _scenario.Viewport.Width,
            height = _scenario.Viewport.Height,
            deviceScaleFactor = _scenario.Viewport.DeviceScaleFactor,
            mobile = _scenario.Viewport.Mobile
        }, cancellationToken);
        await SendAsync("Emulation.setEmulatedMedia", new
        {
            features = new[] { new { name = "prefers-color-scheme", value = _scenario.ColorScheme } }
        }, cancellationToken);
        // The hide list before the first paint of every page: installed only after load, a hidden
        // element (the feedback button) flashed on screen for the first frames of each navigation.
        var hide = JsonSerializer.Serialize(string.Join(",", _scenario.Hide.Append("nextjs-portal")) + "{display:none !important}");
        await SendAsync("Page.addScriptToEvaluateOnNewDocument", new
        {
            // It runs before the document has a root element, so the observer puts the style in as soon as
            // there is one, and again whenever hydration drops it.
            source = "(() => { const put = () => { if (document.getElementById('__promo_hide')) return; " +
                     "const root = document.head || document.documentElement; if (!root) return; " +
                     "const s = document.createElement('style'); s.id = '__promo_hide'; s.textContent = " + hide + "; " +
                     "root.appendChild(s); }; put(); " +
                     "new MutationObserver(put).observe(document, { childList: true, subtree: true }); })()"
        }, cancellationToken);

        // The site does not follow prefers-color-scheme: its boot script reads the `theme` cookie
        // the theme switcher writes (web/src/lib/theme/theme.ts), so set that too.
        await SendAsync("Network.setCookie", new
        {
            name = "theme",
            value = _scenario.ColorScheme,
            url = new Uri(_scenario.BaseUrl).GetLeftPart(UriPartial.Authority),
            path = "/"
        }, cancellationToken);
        await SendAsync("Emulation.setLocaleOverride", new { locale = _scenario.Language == "tr" ? "tr-TR" : "en-US" }, cancellationToken);
    }

    private Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken cancellationToken) =>
        _cdp.SendAsync(method, parameters, _session, cancellationToken);

    // ---- page actions ----------------------------------------------------------------------------

    public async Task GotoAsync(string path, CancellationToken cancellationToken)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _cdp.On("Page.loadEventFired", _ => loaded.TrySetResult());
        var url = new Uri(new Uri(_scenario.BaseUrl), path).AbsoluteUri;
        await SendAsync("Page.navigate", new { url }, cancellationToken);
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        // Hydration and the first data fetches settle after load.
        await Task.Delay(800, cancellationToken);
        await EnsureOverlayAsync(cancellationToken);
    }

    public async Task ClickAsync(string target, CancellationToken cancellationToken)
    {
        var (x, y) = await LocateAsync(target, cancellationToken);
        await MoveCursorAsync(x, y, cancellationToken);
        await EvaluateAsync("window.__promo && window.__promo.press()", cancellationToken);
        foreach (var type in new[] { "mousePressed", "mouseReleased" })
        {
            await SendAsync("Input.dispatchMouseEvent", new { type, x, y, button = "left", clickCount = 1 }, cancellationToken);
        }

        await Task.Delay(500, cancellationToken);
        await EnsureOverlayAsync(cancellationToken);
    }

    public async Task HoverAsync(string target, CancellationToken cancellationToken)
    {
        var (x, y) = await LocateAsync(target, cancellationToken);
        await MoveCursorAsync(x, y, cancellationToken);
    }

    /// <summary>Per-character delay when a step gives none: slow enough that a viewer can read the
    /// value as it goes in (the user found faster typing hard to follow).</summary>
    public const int ReadableTypingDelayMs = 120;

    public async Task TypeAsync(string target, string text, int delayMs, bool clear, CancellationToken cancellationToken)
    {
        await ClickAsync(target, cancellationToken);
        if (clear)
        {
            // Inserted text replaces a selection, so the old value goes the way it would by hand.
            await EvaluateAsync($"(() => {{ const el = {FindScript(target)}; if (el && el.select) el.select(); }})()", cancellationToken);
        }

        foreach (var character in text)
        {
            await SendAsync("Input.insertText", new { text = character.ToString() }, cancellationToken);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        // A beat on the finished value before the cursor moves on, so it can be read; not for the
        // off-camera sign-in, which types with no delay.
        if (delayMs > 0)
        {
            await Task.Delay(600, cancellationToken);
        }
    }

    public async Task PressAsync(string key, CancellationToken cancellationToken)
    {
        var (code, keyCode) = key switch
        {
            "Enter" => ("Enter", 13),
            "Escape" => ("Escape", 27),
            "Tab" => ("Tab", 9),
            "ArrowDown" => ("ArrowDown", 40),
            "ArrowUp" => ("ArrowUp", 38),
            "Backspace" => ("Backspace", 8),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported key.")
        };
        object keyDown = key == "Enter"
            ? new { type = "keyDown", key, code, windowsVirtualKeyCode = keyCode, text = "\r" }
            : new { type = "keyDown", key, code, windowsVirtualKeyCode = keyCode };
        await SendAsync("Input.dispatchKeyEvent", keyDown, cancellationToken);
        await SendAsync("Input.dispatchKeyEvent", new { type = "keyUp", key, code, windowsVirtualKeyCode = keyCode }, cancellationToken);
        await Task.Delay(300, cancellationToken);
    }

    public async Task ScrollAsync(string? target, int? by, CancellationToken cancellationToken)
    {
        var script = target is not null
            ? $"(() => {{ const el = {FindScript(target)}; if (!el) throw new Error('not found'); el.scrollIntoView({{ behavior: 'smooth', block: 'center' }}); }})()"
            : $"window.scrollBy({{ top: {by}, behavior: 'smooth' }})";
        await EvaluateAsync(script, cancellationToken);
        await Task.Delay(1200, cancellationToken);
    }

    /// <summary>Picks up <paramref name="target"/>, carries it to <paramref name="to"/> and lets go —
    /// real mouse events with the button held, so drag-and-drop libraries (the board's dnd-kit, which
    /// starts a drag after 6 px) see a person dragging. Slow enough for a viewer to follow.</summary>
    public async Task DragAsync(string target, string to, CancellationToken cancellationToken)
    {
        var (fromX, fromY) = await LocateAsync(target, cancellationToken);
        await MoveCursorAsync(fromX, fromY, cancellationToken);
        var (toX, toY) = await CenterOfAsync(to, cancellationToken);

        await SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x = fromX, y = fromY, button = "left", buttons = 1, clickCount = 1 },
            cancellationToken);
        await Task.Delay(250, cancellationToken);
        const int steps = 40;
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var eased = t * t * (3 - 2 * t);
            var (x, y) = (fromX + (toX - fromX) * eased, fromY + (toY - fromY) * eased);
            await EvaluateAsync(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"window.__promo && window.__promo.move({x}, {y})"), cancellationToken);
            await SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x, y, button = "left", buttons = 1 }, cancellationToken);
            await Task.Delay(35, cancellationToken);
        }

        await Task.Delay(300, cancellationToken);
        await SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x = toX, y = toY, button = "left", buttons = 0, clickCount = 1 },
            cancellationToken);
        _cursor = (toX, toY);
        await Task.Delay(600, cancellationToken);
        await EnsureOverlayAsync(cancellationToken);
    }

    /// <summary>The on-screen centre of a target, without scrolling to it (a drop target is already
    /// in view, and scrolling mid-drag would move what is being carried).</summary>
    private async Task<(double X, double Y)> CenterOfAsync(string target, CancellationToken cancellationToken)
    {
        await WaitForAsync(target, 10, cancellationToken);
        var box = await EvaluateAsync(
            $"(() => {{ const r = {FindScript(target)}.getBoundingClientRect(); return [r.left + r.width / 2, r.top + Math.min(r.height / 2, 160)]; }})()",
            cancellationToken);
        return (box[0].GetDouble(), box[1].GetDouble());
    }

    /// <summary>Hands <paramref name="file"/> to a file input, as picking it in the file dialog would.
    /// The input is usually hidden behind a drop zone, so there is nothing for the cursor to move
    /// to; a hover on the drop zone before this step reads as the drop.</summary>
    public async Task UploadAsync(string target, string file, CancellationToken cancellationToken)
    {
        var result = await SendAsync("Runtime.evaluate", new
        {
            expression = $"(() => {{ const el = {FindScript(target)}; return el instanceof HTMLInputElement && el.type === 'file' ? el : null; }})()",
            returnByValue = false
        }, cancellationToken);
        if (!result.GetProperty("result").TryGetProperty("objectId", out var objectId))
        {
            throw new InvalidOperationException($"'{target}' is not a file input on this page.");
        }

        // Chrome fires the input's change event itself, so the page reacts as it would to a real pick.
        await SendAsync("DOM.setFileInputFiles", new { files = new[] { file }, objectId = objectId.GetString() }, cancellationToken);
        await Task.Delay(500, cancellationToken);
    }

    public async Task WaitForAsync(string target, double seconds, CancellationToken cancellationToken)
    {
        var deadline = Now + seconds;
        while (Now < deadline)
        {
            var found = await EvaluateAsync($"(() => {{ const el = {FindScript(target)}; return !!el && el.getClientRects().length > 0; }})()",
                cancellationToken);
            if (found.ValueKind == JsonValueKind.True)
            {
                return;
            }

            await Task.Delay(150, cancellationToken);
        }

        throw new TimeoutException($"'{target}' did not appear within {seconds} seconds.");
    }

    /// <summary>Signs in through the login form before recording starts, so the credentials are
    /// never on film. The values come from the environment, never from the scenario file.</summary>
    public async Task SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        await GotoAsync($"/{_scenario.Language}/login", cancellationToken);
        await WaitForAsync("input[autocomplete=email]", 15, cancellationToken);
        await TypeAsync("input[autocomplete=email]", email, 0, clear: false, cancellationToken);
        await TypeAsync("input[autocomplete=current-password]", password, 0, clear: false, cancellationToken);
        await ClickAsync("button[type=submit]", cancellationToken);

        var deadline = Now + 20;
        while (Now < deadline)
        {
            var path = await EvaluateAsync("location.pathname", cancellationToken);
            if (!path.GetString()!.EndsWith("/login", StringComparison.Ordinal))
            {
                await Task.Delay(1000, cancellationToken);
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new InvalidOperationException("Sign-in did not leave the login page — check PROMO_EMAIL / PROMO_PASSWORD.");
    }

    /// <summary>A caption drawn into the page itself, so it is burned into the frames without
    /// needing an ffmpeg built with libass. Null clears it.</summary>
    public async Task ShowCaptionAsync(string? text, CancellationToken cancellationToken)
    {
        await EnsureOverlayAsync(cancellationToken);
        await EvaluateAsync($"window.__promo && window.__promo.caption({JsonSerializer.Serialize(text)})", cancellationToken);
    }

    /// <summary>
    /// A full-screen title card over the page — the opening and closing of a video: the brand mark
    /// (served by the site itself), a title and an optional line under it, fading in. It stays
    /// until <see cref="HideCardAsync"/> or the next page load. Built with DOM APIs only.
    /// </summary>
    public async Task ShowCardAsync(string title, string? text, CancellationToken cancellationToken)
    {
        // The card follows the video's theme, so a dark video does not open on a white flash.
        var dark = _scenario.ColorScheme == "dark";
        var background = dark
            ? "radial-gradient(ellipse at 50% 40%,#1b2536 0%,#111827 55%,#0b1120 100%)"
            : "radial-gradient(ellipse at 50% 40%,#ffffff 0%,#eef3fe 55%,#dde7fb 100%)";
        var headingColor = dark ? "#f3f4f6" : "#0f172a";
        var lineColor = dark ? "#7ea6f4" : "#2a5fd6";
        await EvaluateAsync($$"""
            (() => {
              document.getElementById('__promo_card')?.remove();
              const card = document.createElement('div');
              card.id = '__promo_card';
              card.style.cssText = 'position:fixed;inset:0;z-index:2147483646;display:flex;flex-direction:column;align-items:center;' +
                'justify-content:center;gap:28px;background:{{background}};' +
                'font-family:Geist,system-ui,sans-serif;opacity:0;transform:scale(1.02);transition:opacity .7s ease,transform 1.2s ease';
              const logo = document.createElement('img');
              logo.src = '/brand/logo-mark.png';
              logo.alt = '';
              logo.style.cssText = 'width:min(22vh,220px);height:auto';
              const heading = document.createElement('div');
              heading.textContent = {{JsonSerializer.Serialize(title)}};
              heading.style.cssText = 'font-size:min(9vh,88px);font-weight:700;letter-spacing:-.02em;color:{{headingColor}};text-align:center;padding:0 6vw';
              card.append(logo, heading);
              const line = {{JsonSerializer.Serialize(text)}};
              if (line) {
                const sub = document.createElement('div');
                sub.textContent = line;
                sub.style.cssText = 'font-size:min(4vh,38px);font-weight:500;color:{{lineColor}};text-align:center;padding:0 8vw;max-width:1400px';
                card.append(sub);
              }
              document.body.appendChild(card);
              return new Promise(resolve => logo.complete ? resolve() : (logo.onload = logo.onerror = () => resolve()));
            })()
            """, cancellationToken);
        await EvaluateAsync("""
            (() => { const card = document.getElementById('__promo_card'); void card.offsetWidth;
                     card.style.opacity = '1'; card.style.transform = 'scale(1)'; })()
            """, cancellationToken);
        await Task.Delay(800, cancellationToken);
    }

    public async Task HideCardAsync(CancellationToken cancellationToken)
    {
        await EvaluateAsync("""
            (() => { const card = document.getElementById('__promo_card'); if (!card) return;
                     card.style.opacity = '0'; setTimeout(() => card.remove(), 800); })()
            """, cancellationToken);
        await Task.Delay(800, cancellationToken);
    }

    private async Task<(double X, double Y)> LocateAsync(string target, CancellationToken cancellationToken)
    {
        await WaitForAsync(target, 10, cancellationToken);
        // Bring it into view first; the smooth scroll is part of what the viewer should see.
        await EvaluateAsync($"(() => {{ const el = {FindScript(target)}; el.scrollIntoView({{ behavior: 'smooth', block: 'center' }}); }})()",
            cancellationToken);
        await Task.Delay(700, cancellationToken);
        var box = await EvaluateAsync(
            $"(() => {{ const r = {FindScript(target)}.getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2]; }})()",
            cancellationToken);
        return (box[0].GetDouble(), box[1].GetDouble());
    }

    private async Task MoveCursorAsync(double x, double y, CancellationToken cancellationToken)
    {
        await EnsureOverlayAsync(cancellationToken);
        // The overlay animates the drawn cursor; real mouse moves follow the same path so hover
        // styles light up on the way.
        await EvaluateAsync(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"window.__promo && window.__promo.move({x}, {y})"), cancellationToken);
        const int steps = 12;
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var eased = t * t * (3 - 2 * t);
            await SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseMoved",
                x = _cursor.X + (x - _cursor.X) * eased,
                y = _cursor.Y + (y - _cursor.Y) * eased
            }, cancellationToken);
            await Task.Delay(45, cancellationToken);
        }

        _cursor = (x, y);
        await Task.Delay(150, cancellationToken);
    }

    /// <summary>JS that finds a target: a CSS selector, or <c>text=Label</c> for the clickable
    /// element whose visible text is exactly (else starts with) the label.</summary>
    private static string FindScript(string target)
    {
        if (target.StartsWith("text=", StringComparison.Ordinal))
        {
            var label = JsonSerializer.Serialize(target[5..].Trim());
            return "(() => { const want = " + label + ".toLocaleLowerCase(); " +
                   "const all = [...document.querySelectorAll('a,button,[role=button],[role=tab],[role=menuitem],[role=option],label,summary,input[type=submit]')]" +
                   ".filter(e => e.getClientRects().length > 0); " +
                   "const text = e => (e.innerText || e.value || '').trim().replace(/\\s+/g, ' ').toLocaleLowerCase(); " +
                   "return all.find(e => text(e) === want) || all.find(e => text(e).startsWith(want)) || null; })()";
        }

        return $"document.querySelector({JsonSerializer.Serialize(target)})";
    }

    private async Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        var result = await SendAsync("Runtime.evaluate", new { expression, awaitPromise = true, returnByValue = true }, cancellationToken);
        if (result.TryGetProperty("exceptionDetails", out var exception))
        {
            var description = exception.TryGetProperty("exception", out var e) && e.TryGetProperty("description", out var d)
                ? d.GetString()
                : exception.GetRawText();
            throw new InvalidOperationException($"Page script failed: {description}");
        }

        return result.GetProperty("result").TryGetProperty("value", out var value) ? value : default;
    }

    /// <summary>
    /// The cursor, the click ripple, the caption box and the hide-list stylesheet. Re-installed
    /// after every navigation and click, because a page load (and sometimes React) drops nodes
    /// it did not render. Built with DOM APIs only — no HTML strings.
    /// </summary>
    private async Task EnsureOverlayAsync(CancellationToken cancellationToken)
    {
        var hide = JsonSerializer.Serialize(string.Join(",", _scenario.Hide.Append("nextjs-portal")));
        var script = """
            (() => {
              const w = window;
              if (!document.body) return;
              if (!document.getElementById('__promo_style')) {
                const style = document.createElement('style');
                style.id = '__promo_style';
                style.textContent = HIDE + '{display:none !important}' +
                  '#__promo_cursor{position:fixed;left:0;top:0;width:22px;height:22px;margin:-11px 0 0 -11px;border-radius:50%;' +
                  'background:rgba(20,20,20,.55);border:2px solid #fff;box-shadow:0 1px 6px rgba(0,0,0,.35);z-index:2147483645;' +
                  'pointer-events:none;transition:transform .6s cubic-bezier(.4,0,.2,1)}' +
                  '#__promo_cursor.press{animation:__promo_press .35s ease-out}' +
                  '@keyframes __promo_press{0%{box-shadow:0 0 0 0 rgba(37,99,235,.6)}100%{box-shadow:0 0 0 18px rgba(37,99,235,0)}}' +
                  '#__promo_caption{position:fixed;left:50%;bottom:6%;transform:translateX(-50%);max-width:80%;padding:10px 18px;' +
                  'border-radius:10px;background:rgba(0,0,0,.72);color:#fff;font:600 28px/1.35 system-ui,sans-serif;text-align:center;' +
                  'z-index:2147483647;pointer-events:none}#__promo_caption:empty{display:none}';
                document.head.appendChild(style);
              }
              let cursor = document.getElementById('__promo_cursor');
              if (!cursor) {
                cursor = document.createElement('div');
                cursor.id = '__promo_cursor';
                cursor.style.transform = 'translate(' + (w.__promoX ?? innerWidth / 2) + 'px,' + (w.__promoY ?? innerHeight / 2) + 'px)';
                document.body.appendChild(cursor);
              }
              let caption = document.getElementById('__promo_caption');
              if (!caption) {
                caption = document.createElement('div');
                caption.id = '__promo_caption';
                caption.textContent = w.__promoCaption ?? '';
                document.body.appendChild(caption);
              }
              w.__promo = {
                move(x, y) { w.__promoX = x; w.__promoY = y; cursor.style.transform = 'translate(' + x + 'px,' + y + 'px)'; },
                press() { cursor.classList.remove('press'); void cursor.offsetWidth; cursor.classList.add('press'); },
                caption(text) { w.__promoCaption = text ?? ''; caption.textContent = w.__promoCaption; }
              };
            })()
            """.Replace("HIDE", hide, StringComparison.Ordinal);
        try
        {
            await EvaluateAsync(script, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // A click that starts a full page load destroys the context mid-call; the next
            // navigation or step installs the overlay again.
        }
    }

    // ---- recording -------------------------------------------------------------------------------

    /// <summary>Starts writing frames into <paramref name="directory"/> and returns once the first
    /// one has arrived, so the video's zero is a real picture.</summary>
    public async Task StartRecordingAsync(string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        _frames = Channel.CreateUnbounded<(string, double)>(new UnboundedChannelOptions { SingleReader = true });
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _cdp.On("Page.screencastFrame", frame =>
        {
            var at = Now;
            if (double.IsNaN(FirstFrameAt))
            {
                FirstFrameAt = at;
            }

            _frames.Writer.TryWrite((frame.GetProperty("data").GetString()!, at));
            // Chrome sends the next frame only after this one is acknowledged; the reply is not
            // awaited here because this runs on the receive loop.
            _ = _cdp.SendAsync("Page.screencastFrameAck", new { sessionId = frame.GetProperty("sessionId").GetInt32() }, _session);
            first.TrySetResult();
        });

        _frameWriter = Task.Run(async () =>
        {
            await foreach (var (data, at) in _frames.Reader.ReadAllAsync(CancellationToken.None))
            {
                var name = $"frame_{_written.Count:D6}.jpg";
                await File.WriteAllBytesAsync(Path.Combine(directory, name), Convert.FromBase64String(data), CancellationToken.None);
                _written.Add((name, at));
            }
        }, CancellationToken.None);

        var scale = _scenario.Viewport.DeviceScaleFactor;
        await SendAsync("Page.startScreencast", new
        {
            format = "jpeg",
            quality = 92,
            maxWidth = (int)(_scenario.Viewport.Width * scale),
            maxHeight = (int)(_scenario.Viewport.Height * scale),
            everyNthFrame = 1
        }, cancellationToken);

        // Nudge a repaint: a static page sends no frame until something changes.
        await EnsureOverlayAsync(cancellationToken);
        await EvaluateAsync("window.__promo && window.__promo.move(window.__promoX ?? innerWidth / 2, window.__promoY ?? innerHeight / 2)", cancellationToken);
        await first.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken)
    {
        await SendAsync("Page.stopScreencast", null, cancellationToken);
        _frames?.Writer.TryComplete();
        if (_frameWriter is not null)
        {
            await _frameWriter;
        }
    }

    /// <summary>What the page looked like when a step failed, for the error message.</summary>
    public async Task ScreenshotAsync(string file, CancellationToken cancellationToken)
    {
        var shot = await SendAsync("Page.captureScreenshot", new { format = "png" }, cancellationToken);
        await File.WriteAllBytesAsync(file, shot.GetProperty("data").GetBytesFromBase64(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _cdp.DisposeAsync();
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(CancellationToken.None);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        _process.Dispose();
        try
        {
            Directory.Delete(_profile, recursive: true);
        }
        catch (IOException)
        {
            // Chrome can hold a file for a moment after exit; the temp folder is cleaned by the OS.
        }
    }
}
