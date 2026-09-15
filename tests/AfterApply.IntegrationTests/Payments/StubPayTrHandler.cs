using System.Net;
using System.Text;

namespace AfterApply.IntegrationTests.Payments;

/// <summary>
/// Stands in for www.paytr.com. Only the transport is replaced — the real client builds the form
/// and the signatures, and the real services act on what comes back. Answers are scripted per
/// path so a test can make get-token succeed and the refund be refused, or the whole host vanish.
/// </summary>
internal sealed class StubPayTrHandler : HttpMessageHandler
{
    private readonly List<(string Path, IReadOnlyDictionary<string, string> Form)> _requests = [];

    public Func<IReadOnlyDictionary<string, string>, HttpResponseMessage> TokenResponse { get; set; } =
        _ => Json("""{"status":"success","token":"stub-iframe-token"}""");

    public Func<IReadOnlyDictionary<string, string>, HttpResponseMessage> RefundResponse { get; set; } =
        form => Json($$"""{"status":"success","is_test":1,"merchant_oid":"{{form["merchant_oid"]}}","return_amount":"{{form["return_amount"]}}","reference_no":"{{form["reference_no"]}}"}""");

    public IReadOnlyList<(string Path, IReadOnlyDictionary<string, string> Form)> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(kv => WebUtility.UrlDecode(kv[0]), kv => kv.Length > 1 ? WebUtility.UrlDecode(kv[1]) : string.Empty);
        var path = request.RequestUri!.AbsolutePath;
        lock (_requests)
        {
            _requests.Add((path, form));
        }

        return path.EndsWith("get-token", StringComparison.Ordinal) ? TokenResponse(form) : RefundResponse(form);
    }
}

/// <summary>A clock the test moves by hand, so the expiry job and the reminder job can be run
/// "later" without waiting.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset now) => _now = now;
}
