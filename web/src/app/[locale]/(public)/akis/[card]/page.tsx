import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";
import { parseFlowCard } from "@/lib/flowCard/card";
import { FLOW_NODE_KEYS } from "@/lib/flowCard/copy";
import { flowCardPath } from "@/lib/flowCard/path";
import { buttonClassName } from "@/components/ui/Button";

/**
 * Where a shared flow card lands: `/tr/akis/<card>` (`/en/flow/<card>` by rewrite). The card itself
 * — the same picture the link preview shows — then what each part of it means, then the one
 * question it asks the reader.
 *
 * The card in the URL is numbers only (`lib/flowCard/card.ts`); one that does not add up is a 404,
 * decided in proxy.ts before this renders (a notFound() here would 500 in production, DECISIONS.md
 * 2026-09-16). Not indexed: every card is one person's numbers, a thousand addresses of the same
 * page to a search engine, and nothing a searcher is looking for — but followed, so the links out of
 * it count. The image route keeps the picture out of image search the same way (X-Robots-Tag).
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/akis/[card]">): Promise<Metadata> {
  const { locale, card: segment } = await params;
  const card = parseFlowCard(segment);
  if (!card) return {};

  const t = await getTranslations({ locale, namespace: "flowCard" });
  const total = new Intl.NumberFormat(locale).format(card.counts.total);
  const path = Object.fromEntries(routing.locales.map((other) => [other, flowCardPath(other, segment)]));
  const image = { url: `/${locale}/og?flow=${segment}&f=link`, width: OG_IMAGE_WIDTH, height: OG_IMAGE_HEIGHT, alt: t("page.imageAlt", { total }) };

  return buildMetadata({
    locale,
    path,
    title: t("page.metaTitle", { total }),
    description: t("page.metaDescription", { total }),
    index: false,
    // Its links are the point of it (sign up, the home page), so they stay followable.
    follow: true,
    image,
  });
}

export default async function SharedFlowPage({ params }: PageProps<"/[locale]/akis/[card]">) {
  const { locale, card: segment } = await params;
  setRequestLocale(locale);

  // The proxy has already turned an invalid card into a 404; this is the type narrowing only.
  const card = parseFlowCard(segment);
  if (!card) return null;

  const t = await getTranslations("flowCard");
  const total = new Intl.NumberFormat(locale).format(card.counts.total);
  // Only the parts this card actually has: a legend line for a node that is not drawn is noise.
  const present = FLOW_NODE_KEYS.filter((key) => key !== "total" && card.counts[key] > 0);

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-10 px-4 py-12">
      <section className="flex flex-col gap-4">
        <h1 className="text-sm font-normal text-gray-500 dark:text-gray-400">{t("page.eyebrow")}</h1>
        {/* The share image itself, so the page shows exactly what the link preview promised — at
            twice the pixels, since at this width the 1200 px preview is stretched on a retina screen. */}
        {/* eslint-disable-next-line @next/next/no-img-element -- a generated PNG from our own route */}
        <img
          src={`/${locale}/og?flow=${segment}&f=link&d=2`}
          width={OG_IMAGE_WIDTH}
          height={OG_IMAGE_HEIGHT}
          alt={t("page.imageAlt", { total })}
          className="h-auto w-full rounded-xl border border-gray-200 dark:border-gray-800"
        />
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("legend.title")}</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("legend.intro")}</p>
        <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
          {present.map((key) => (
            <div key={key} className="flex flex-col">
              <dt className="font-medium text-gray-900 dark:text-gray-100">{t(`card.nodes.${key}`)}</dt>
              <dd className="text-gray-600 dark:text-gray-400">{t(`legend.items.${key}`)}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-gray-50 p-6 dark:border-gray-800 dark:bg-gray-900">
        <h2 className="text-2xl font-semibold tracking-tight text-gray-900 sm:text-3xl dark:text-gray-100">{t("page.heading")}</h2>
        <p className="max-w-[62ch] text-gray-700 dark:text-gray-300">{t("page.body")}</p>
        <div className="flex flex-wrap gap-3">
          <Link href="/register" className={buttonClassName("primary", "px-5 py-2.5")}>
            {t("page.cta")}
          </Link>
          <Link href="/" className={buttonClassName("secondary", "px-5 py-2.5")}>
            {t("page.secondary")}
          </Link>
        </div>
      </section>
    </div>
  );
}
