import PostalMime from "postal-mime";

const SNIPPET_MAX_LENGTH = 300;

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

    const payload = {
      to: message.to,
      from: message.from,
      fromName: parsed.from?.name ?? "",
      subject,
      snippet,
      receivedAt,
    };

    let response;
    try {
      response = await fetch(env.INBOUND_WEBHOOK_URL, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Webhook-Secret": env.INBOUND_WEBHOOK_SECRET,
          "X-Inbound-Token": token ?? "",
        },
        body: JSON.stringify(payload),
      });
    } catch (err) {
      // No custom retry loop — Cloudflare's own Worker exception handling/retry behavior is the
      // defense here, not hand-rolled backoff.
      console.error("Inbound webhook request failed", err);
      return;
    }

    if (!response.ok) {
      console.error(`Inbound webhook rejected: ${response.status} ${await response.text()}`);
    }
  },
};

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
