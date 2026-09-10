namespace AfterApply.IntegrationTests;

/// <summary>
/// A real socket, for the one test that is allowed one.
///
/// <see cref="NoOutboundHttpStartup"/> blocks outbound HTTP by replacing any primary handler that
/// is an <c>HttpClientHandler</c> or a <c>SocketsHttpHandler</c> — the two types that can actually
/// dial out — and leaving anything else alone, so that a test's stub survives. Registering a bare
/// <c>SocketsHttpHandler</c> to *undo* the block therefore does not work: it is exactly what the
/// filter is looking for.
///
/// This wrapper is a different type, so it survives, and it forwards to a real handler. It exists
/// for <c>CvReviewEvalTests</c> alone, which is opt-in behind an environment variable and whose
/// entire purpose is to call a real API. <b>Do not reach for it anywhere else</b>: every other test
/// in this assembly should be stubbing its dependency, and a test that quietly starts making real
/// requests is the failure mode NoOutboundHttpStartup was written to end.
/// </summary>
internal sealed class CvReviewEvalRealNetworkHandler() : DelegatingHandler(new SocketsHttpHandler());
