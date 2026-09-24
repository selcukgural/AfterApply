import { headers } from "next/headers";

/**
 * Server-side renders reach the API from the web service's own address, which every visitor would
 * share: one crawler asking for pages could spend the rate limit of everyone reading them. So a
 * render that fetches on every request (2026-09-24) names the visitor it renders for, and proves it
 * is the web app doing the naming with a secret both services hold (`API_SERVER_RENDER_KEY` here,
 * `RateLimiting:ServerRenderKey` on the API — see the API's ClientPartition). The API then counts
 * the request against that visitor.
 *
 * The secret is a server-only environment variable (no NEXT_PUBLIC_ prefix), so it never reaches a
 * browser bundle. Without it — local development, tests — no header is sent and the API counts the
 * request as before. Only for fetches that are dynamic already: reading the request's headers opts
 * a page out of static rendering.
 */
const RENDER_KEY = process.env.API_SERVER_RENDER_KEY;

export async function renderHeaders(): Promise<Record<string, string>> {
  if (!RENDER_KEY) {
    return {};
  }

  let visitor: string | undefined;
  try {
    // Cloud Run appends the connecting address last; earlier entries are whatever the client sent.
    visitor = (await headers())
      .get("x-forwarded-for")
      ?.split(",")
      .map((part) => part.trim())
      .filter(Boolean)
      .at(-1);
  } catch {
    // Outside a request (a build-time or revalidation render): nobody to name.
    return {};
  }

  return visitor ? { "X-Render-Key": RENDER_KEY, "X-Render-Client": visitor } : {};
}
