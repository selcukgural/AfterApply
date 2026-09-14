import { ImageResponse } from "next/og";
import type { NextRequest } from "next/server";
import { hasLocale } from "next-intl";
import { routing } from "@/i18n/routing";
import { OgCard } from "@/components/seo/OgCard";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH, OG_KICKER_MAX_LENGTH, OG_TITLE_MAX_LENGTH, sanitizeOgText } from "@/lib/seo/ogImage";

/**
 * `GET /{locale}/og?t=<title>&k=<kicker>` — the share card for every public page that is not the
 * landing page (which keeps its file-convention `opengraph-image.tsx`, drawn with the same card).
 *
 * The title comes from the URL, and the URL is public, so the two inputs are clamped and stripped
 * before they reach the renderer (`sanitizeOgText`) and an unknown locale is a 404 rather than a
 * card. A day of caching: a crawler re-fetches the image every time it re-reads the page, and the
 * card for a given title never changes.
 */
export async function GET(request: NextRequest, context: RouteContext<"/[locale]/og">) {
  const { locale } = await context.params;
  if (!hasLocale(routing.locales, locale)) {
    return new Response("Not found", { status: 404 });
  }

  const params = request.nextUrl.searchParams;
  const title = sanitizeOgText(params.get("t"), OG_TITLE_MAX_LENGTH) || "e-kariyerim";
  const kicker = sanitizeOgText(params.get("k"), OG_KICKER_MAX_LENGTH) || undefined;

  return new ImageResponse(<OgCard title={title} kicker={kicker} />, {
    width: OG_IMAGE_WIDTH,
    height: OG_IMAGE_HEIGHT,
    headers: { "Cache-Control": "public, max-age=86400, s-maxage=86400" },
  });
}
