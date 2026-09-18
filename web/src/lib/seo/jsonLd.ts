import { SITE_NAME, SITE_URL } from "./routes";
import { SOCIAL_LINKS } from "@/lib/constants/socialLinks";

/**
 * schema.org descriptions of the site, emitted as JSON-LD.
 *
 * None of this changes what a page looks like; it tells Google *what the page is* rather than
 * leaving it to infer that from prose. The Organization node is what ties the domain, the name and
 * the logo together as one entity, and the breadcrumb trail is what turns the URL line under a help
 * result into "e-kariyerim › Yardım Merkezi › Chrome Eklentisi".
 *
 * Deliberately not here: FAQPage. Google has restricted FAQ rich results to government and health
 * sites since 2023, so marking up the twelve questions on /help/faq would buy nothing.
 */

const ORGANIZATION_ID = `${SITE_URL}/#organization`;

export type JsonLdNode = Record<string, unknown>;

export function organizationJsonLd(): JsonLdNode {
  return {
    "@type": "Organization",
    "@id": ORGANIZATION_ID,
    name: SITE_NAME,
    url: SITE_URL,
    logo: `${SITE_URL}/brand/logo-mark.png`,
    // The product's own accounts — what lets a search engine tie the name to the site when
    // "kariyerim" belongs to someone else's product (growth audit finding 07).
    sameAs: SOCIAL_LINKS.map((link) => link.href),
  };
}

export function webApplicationJsonLd(locale: string, description: string): JsonLdNode {
  return {
    "@type": "WebApplication",
    "@id": `${SITE_URL}/#webapp`,
    name: SITE_NAME,
    url: `${SITE_URL}/${locale}`,
    description,
    inLanguage: locale,
    applicationCategory: "BusinessApplication",
    operatingSystem: "Web",
    publisher: { "@id": ORGANIZATION_ID },
    // The product is free to use; saying so explicitly is what keeps Google from guessing a price.
    offers: { "@type": "Offer", price: "0", priceCurrency: "TRY" },
  };
}

export type BreadcrumbStep = { name: string; path: string };

export function breadcrumbJsonLd(locale: string, trail: readonly BreadcrumbStep[]): JsonLdNode {
  return {
    "@type": "BreadcrumbList",
    itemListElement: trail.map((step, index) => ({
      "@type": "ListItem",
      position: index + 1,
      name: step.name,
      item: `${SITE_URL}/${locale}${step.path}`,
    })),
  };
}

export type ArticleJsonLdInput = {
  locale: string;
  path: string;
  headline: string;
  description: string;
  datePublished: string;
  dateModified?: string;
  /** Absolute URL of the share card. Google's Article result wants an image; the card is the one
   *  every article has. */
  image: string;
};

/**
 * `author` and `publisher` point at the Organization node by `@id`, so the page that emits this
 * must put `organizationJsonLd()` in the same `@graph` — until 2026-09-14 the guide pages did not,
 * and the reference dangled: only the landing page carried the Organization node.
 */
export function articleJsonLd({
  locale,
  path,
  headline,
  description,
  datePublished,
  dateModified,
  image,
}: ArticleJsonLdInput): JsonLdNode {
  const url = `${SITE_URL}/${locale}${path}`;
  return {
    "@type": "Article",
    "@id": `${url}#article`,
    headline,
    description,
    image: [image],
    inLanguage: locale,
    datePublished,
    dateModified: dateModified ?? datePublished,
    mainEntityOfPage: { "@type": "WebPage", "@id": url },
    author: { "@id": ORGANIZATION_ID },
    publisher: { "@id": ORGANIZATION_ID },
  };
}

/** Wraps the nodes of one page into a single `@graph`, so `@id` references resolve between them. */
export function jsonLdGraph(...nodes: JsonLdNode[]): JsonLdNode {
  return { "@context": "https://schema.org", "@graph": nodes };
}

/**
 * Serialises a node for a `<script type="application/ld+json">` body.
 *
 * `</script>` inside a string value would end the script element early and let whatever followed be
 * parsed as markup. Everything we put in these nodes today is our own translated copy, but escaping
 * the three characters that can break out costs nothing and keeps that true if a value ever starts
 * coming from a company name or a job title.
 */
export function serializeJsonLd(node: JsonLdNode): string {
  return JSON.stringify(node).replace(/</g, "\\u003c").replace(/>/g, "\\u003e").replace(/&/g, "\\u0026");
}
