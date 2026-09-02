import PostalMime from "postal-mime";

// Long enough to carry a job's location/description, not just enough for the interview/reject
// keyword phrases RuleBasedEmailClassifier looks for near the top of an email — the "new job"
// extraction flow (EmailForwardingService, unmatched-but-signal-bearing email) reads this same
// snippet to pull company/title/location/description out of an email for a job that isn't
// registered yet. Must stay in sync with EmailSuggestionConfiguration's Snippet column length.
const SNIPPET_MAX_LENGTH = 2000;

// Recruitment-signal evidence for the backend's RecruitmentSignalAnalyzer (known ATS/calendar
// link domains) — capped so a marketing-heavy HTML email with dozens of tracking links doesn't
// blow up the payload.
const MAX_LINK_DOMAINS = 20;
const HREF_REGEX = /<a\b[^>]*href\s*=\s*["']?([^\s"'>]+)/gi;

// Bounded retry for transient network/5xx blips against the inbound webhook. Kept small since an
// Email Worker's execution budget is limited — this is not a durable retry buffer, just enough to
// ride out a brief hiccup. Deliberately never throws out of email(): an uncaught exception here
// can make Cloudflare bounce the message back to the original sender (a recruiter/ATS), which is
// worse than silently dropping it.
const WEBHOOK_MAX_ATTEMPTS = 3;
const WEBHOOK_RETRY_BASE_DELAY_MS = 300;

export default {
  async email(message, env, ctx) {
    const token = extractLocalPart(message.to);

    let parsed;
    try {
      parsed = await PostalMime.parse(message.raw);
    } catch (err) {
      console.error("Failed to parse inbound email", err);
      return;
    }

    const subject = parsed.subject ?? "";
    const snippet = (parsed.text ?? "").trim().slice(0, SNIPPET_MAX_LENGTH);
    const receivedAt = parseDateHeader(message.headers.get("date"));
    const linkDomains = extractLinkDomains(parsed.html);

    const payload = {
      to: message.to,
      from: message.from,
      fromName: parsed.from?.name ?? "",
      subject,
      snippet,
      receivedAt,
      linkDomains,
    };

    let lastError;
    for (let attempt = 1; attempt <= WEBHOOK_MAX_ATTEMPTS; attempt++) {
      try {
        const response = await fetch(env.INBOUND_WEBHOOK_URL, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Webhook-Secret": env.INBOUND_WEBHOOK_SECRET,
            "X-Inbound-Token": token ?? "",
          },
          body: JSON.stringify(payload),
        });

        if (response.ok) {
          return;
        }

        lastError = new Error(`Inbound webhook rejected: ${response.status} ${await response.text()}`);
      } catch (err) {
        lastError = err;
      }

      if (attempt < WEBHOOK_MAX_ATTEMPTS) {
        await sleep(WEBHOOK_RETRY_BASE_DELAY_MS * attempt);
      }
    }

    // Retries exhausted — API is unreachable or persistently rejecting. No durable buffer
    // (e.g. a Cloudflare Queue) backs this yet, so the email is dropped here; only visible
    // via this log line, not via the Email Routing "Delivery failed" metric.
    console.error(`Inbound webhook delivery failed after ${WEBHOOK_MAX_ATTEMPTS} attempts`, lastError);
  },
};

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// Hostnames only, never full URLs — a query string can carry per-recipient tracking tokens (PII),
// and RecruitmentSignalAnalyzer only ever needs to know *which service* a link points at (greenhouse,
// calendly, ...), not the link itself.
function extractLinkDomains(html) {
  if (!html) {
    return [];
  }

  const domains = new Set();
  for (const match of html.matchAll(HREF_REGEX)) {
    let hostname;
    try {
      hostname = new URL(match[1]).hostname.toLowerCase();
    } catch {
      // Not a parseable absolute URL (relative href, etc.) — not a link-domain signal.
      continue;
    }

    // mailto:/cid:/tel: etc. parse successfully but have no hostname — not a link-domain signal.
    if (hostname) {
      domains.add(hostname);
    }

    if (domains.size >= MAX_LINK_DOMAINS) {
      break;
    }
  }

  return [...domains];
}

function extractLocalPart(address) {
  const at = address.indexOf("@");
  return at > 0 ? address.slice(0, at).toLowerCase() : null;
}

function parseDateHeader(value) {
  if (!value) {
    return null;
  }
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}
