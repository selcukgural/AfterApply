import { getLocale, getTranslations } from "next-intl/server";
import { JsonLd } from "@/components/seo/JsonLd";
import { breadcrumbJsonLd, jsonLdGraph, type BreadcrumbStep } from "@/lib/seo/jsonLd";
import { HELP_TOPICS, SITE_NAME } from "@/lib/seo/routes";

/**
 * The breadcrumb trail for one help page, as structured data only — the visible sidebar already
 * shows where the reader is. `path` is the locale-less path, the same string the page passes to
 * `pageMetadata`.
 */
export async function HelpBreadcrumbJsonLd({ path }: { path: string }) {
  const locale = await getLocale();
  const t = await getTranslations("help.sidebar");
  // The middle step names the section, not the sidebar link: "Yardım Merkezi" is what belongs in
  // a result's breadcrumb line, where the sidebar's "Genel Bakış" would be meaningless.
  const tSection = await getTranslations("metadata.pages");

  const trail: BreadcrumbStep[] = [
    { name: SITE_NAME, path: "" },
    { name: tSection("help.title"), path: "/help" },
  ];

  const topic = HELP_TOPICS.find((entry) => entry.href === path);
  if (topic && topic.href !== "/help") {
    trail.push({ name: t(topic.key), path: topic.href });
  }

  return <JsonLd data={jsonLdGraph(breadcrumbJsonLd(locale, trail))} />;
}
