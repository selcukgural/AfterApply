import { readFile } from "node:fs/promises";
import { join } from "node:path";
import { ImageResponse } from "next/og";
import type { NextRequest } from "next/server";
import { hasLocale } from "next-intl";
import { getTranslations } from "next-intl/server";
import { routing } from "@/i18n/routing";
import { OgCard } from "@/components/seo/OgCard";
import { ScoreOgCard } from "@/components/seo/ScoreOgCard";
import { FlowOgCard } from "@/components/seo/FlowOgCard";
import { formatMonthRange, parseFlowCard } from "@/lib/flowCard/card";
import { FLOW_NODE_KEYS, flowHeadline, flowSubline } from "@/lib/flowCard/copy";
import { FLOW_FORMAT_SPECS, parseFlowFormat } from "@/lib/flowCard/formats";
import type { FlowNodeKey } from "@/lib/flowCard/layout";
import { parseScoreCard } from "@/lib/cvScan/scoreCard";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH, OG_KICKER_MAX_LENGTH, OG_TITLE_MAX_LENGTH, sanitizeOgText } from "@/lib/seo/ogImage";

/**
 * `GET /{locale}/og?t=<title>&k=<kicker>` — the share card for every public page that is not the
 * landing page (which keeps its file-convention `opengraph-image.tsx`, drawn with the same card).
 * `GET /{locale}/og?flow=<card>&f=link|x|portrait|story[&d=2]` — the shareable application-flow card
 * (`lib/flowCard/card.ts`) at one of its four sizes, at twice the pixels with `d=2`; a card whose
 * columns do not add up is a 404.
 * `GET /{locale}/og?card=88-34-22-13-19` — the shared-score card for a `/cv-tarama/puan/<card>`
 * page (`lib/cvScan/scoreCard.ts`): numbers only, and a card that does not add up is a 404
 * rather than a picture of a score the scan never gave.
 *
 * The title comes from the URL, and the URL is public, so the two inputs are clamped and stripped
 * before they reach the renderer (`sanitizeOgText`) and an unknown locale is a 404 rather than a
 * card. A day of caching: a crawler re-fetches the image every time it re-reads the page, and the
 * card for a given title never changes.
 */
// The flow card carries the real brand mark (the dark cards draw theirs with boxes). Read once per
// server process from public/, which the Docker image ships next to the standalone server.
const flowLogoSrc = `data:image/png;base64,${await readFile(join(process.cwd(), "public/brand/logo-mark-og.png"), "base64")}`;

// And the site's own typeface: satori's bundled font has one weight, so the card's bold headline
// and counts would render regular. Geist (SIL OFL, public/fonts/geist/OFL.txt) in the three weights
// the card uses — about 220 KB, read once.
const readFont = (file: string) => readFile(join(process.cwd(), "public/fonts/geist", file));
const [geistRegular, geistSemiBold, geistBold] = await Promise.all([
  readFont("Geist-Regular.ttf"),
  readFont("Geist-SemiBold.ttf"),
  readFont("Geist-Bold.ttf"),
]);
const flowFonts = [
  { name: "Geist", data: geistRegular, weight: 400 as const, style: "normal" as const },
  { name: "Geist", data: geistSemiBold, weight: 600 as const, style: "normal" as const },
  { name: "Geist", data: geistBold, weight: 700 as const, style: "normal" as const },
];

export async function GET(request: NextRequest, context: RouteContext<"/[locale]/og">) {
  const { locale } = await context.params;
  if (!hasLocale(routing.locales, locale)) {
    return new Response("Not found", { status: 404 });
  }

  const params = request.nextUrl.searchParams;
  const headers = { "Cache-Control": "public, max-age=86400, s-maxage=86400" };

  const flowParam = params.get("flow");
  if (flowParam !== null) {
    const card = parseFlowCard(flowParam);
    const format = parseFlowFormat(params.get("f") ?? "link");
    if (!card || !format) {
      return new Response("Not found", { status: 404 });
    }

    const t = await getTranslations({ locale, namespace: "flowCard.card" });
    const names = Object.fromEntries(FLOW_NODE_KEYS.map((key) => [key, t(`nodes.${key}`)])) as Record<FlowNodeKey, string>;
    const spec = FLOW_FORMAT_SPECS[format];
    // `d=2` doubles the pixels for screens and downloads; anything else is the standard size.
    const density = params.get("d") === "2" ? 2 : 1;
    return new ImageResponse(
      (
        <FlowOgCard
          format={format}
          counts={card.counts}
          logoSrc={flowLogoSrc}
          headline={flowHeadline(card, locale, t)}
          subline={flowSubline(card, t)}
          dateRange={formatMonthRange(card.from, card.to, locale)}
          headers={[t("columns.applications"), t("columns.firstOutcome"), t("columns.afterInterview")]}
          names={names}
          cta={t("cta")}
          footnote={t("footnote")}
          density={density}
        />
      ),
      {
        width: spec.width * density,
        height: spec.height * density,
        // One person's numbers: fine as a link preview, not something to surface in image search.
        // The card's page is noindex; the picture needs its own header, since it can be linked to
        // on its own (Google: robots meta tag / X-Robots-Tag for non-HTML resources).
        headers: { ...headers, "X-Robots-Tag": "noindex" },
        fonts: flowFonts,
      },
    );
  }

  const cardParam = params.get("card");
  if (cardParam !== null) {
    const card = parseScoreCard(cardParam);
    if (!card) {
      return new Response("Not found", { status: 404 });
    }

    const t = await getTranslations({ locale, namespace: "cvScan" });
    return new ImageResponse(
      (
        <ScoreOgCard
          score={card.score}
          categories={(card.categories ?? []).map((entry) => ({
            label: t(`categories.${entry.category}`),
            score: entry.score,
            weight: entry.weight,
          }))}
          kicker={t("shared.card.kicker")}
          title={t("shared.card.title")}
          footer={t("shared.card.footer")}
        />
      ),
      { width: OG_IMAGE_WIDTH, height: OG_IMAGE_HEIGHT, headers },
    );
  }

  const title = sanitizeOgText(params.get("t"), OG_TITLE_MAX_LENGTH) || "e-kariyerim";
  const kicker = sanitizeOgText(params.get("k"), OG_KICKER_MAX_LENGTH) || undefined;

  return new ImageResponse(<OgCard title={title} kicker={kicker} />, {
    width: OG_IMAGE_WIDTH,
    height: OG_IMAGE_HEIGHT,
    headers,
  });
}
