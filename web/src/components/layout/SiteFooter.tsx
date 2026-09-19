import { getLocale, getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { Logo } from "@/components/layout/Logo";
import { CHROME_WEB_STORE_URL } from "@/lib/constants/chromeWebStore";
import { CV_SCAN_PATHS } from "@/lib/cvScan/path";
import { ABOUT_PATHS } from "@/lib/about/path";
import { CONTACT_EMAIL, SOCIAL_LINKS } from "@/lib/constants/socialLinks";
import { SocialIcon } from "@/components/layout/SocialIcon";
import { pathFor, type LocalisedPath } from "@/lib/seo/routes";
import { fetchPublicConfig } from "@/lib/config/publicConfig.server";

/** The landing page's sections — the anchors resolve from any page — and the store listing. */
const PRODUCT_LINKS = [
  { href: "/#how-it-works", key: "howItWorks" },
  { href: "/#extension", key: "extension" },
  { href: "/#features", key: "features" },
] as const;

/** Every public page a visitor can browse, under the same names the header uses. */
type ExploreLink = { href: LocalisedPath; key: "companies" | "cvScan" | "benchmark" | "guide" | "blog" | "help" | "about" };

const EXPLORE_LINKS: readonly ExploreLink[] = [
  { href: "/companies", key: "companies" },
  // The scan's and the about page's slugs are translated; every other page is the same in both.
  { href: CV_SCAN_PATHS, key: "cvScan" },
  { href: "/benchmark", key: "benchmark" },
  { href: "/guide", key: "guide" },
  { href: "/help", key: "help" },
  { href: ABOUT_PATHS, key: "about" },
];

/** The blog, between the guide and the help centre, only once there is a published post — the
 *  header's rule (SiteHeader.siteLinksFor), read here from the server-side config. */
function exploreLinksFor(hasBlog: boolean): readonly ExploreLink[] {
  if (!hasBlog) return EXPLORE_LINKS;
  const helpIndex = EXPLORE_LINKS.findIndex((link) => link.key === "help");
  return [...EXPLORE_LINKS.slice(0, helpIndex), { href: "/blog", key: "blog" }, ...EXPLORE_LINKS.slice(helpIndex)];
}

const LEGAL_LINKS = [
  { href: "/privacy", key: "privacy" },
  { href: "/extension-privacy", key: "extensionPrivacy" },
  { href: "/cookies", key: "cookies" },
  { href: "/terms", key: "terms" },
  // The Pro plan's sale terms. Listed regardless of the payments flag: a consumer (and PayTR's
  // merchant review) must be able to find them without first reaching the checkout.
  { href: "/terms-of-sale", key: "termsOfSale" },
  { href: "/refund-policy", key: "refundPolicy" },
] as const;

/**
 * The footer every signed-out surface shares — the landing page and every page under `(public)`,
 * which had none before 2026-09-13. Three columns since 2026-09-17: the landing's sections,
 * the pages to browse, and the legal texts — the last had been six lines at the bottom of a
 * ten-line "Resources" list that also held the tools and the guide. Product links go through
 * next-intl's Link with a leading `/`, so the landing's section anchors resolve from any page.
 * Since 2026-09-18 the brand column also carries the contact address and the product's own
 * accounts (growth audit finding 11) — the same list the Organization JSON-LD's `sameAs` reads.
 */
export async function SiteFooter() {
  const t = await getTranslations("landing.footer");
  const tNav = await getTranslations("siteNav");
  const locale = await getLocale();
  const year = new Date().getFullYear();
  // Null when the API is unreachable: the footer then simply has no blog link.
  const config = await fetchPublicConfig();
  const exploreLinks = exploreLinksFor(config?.blog?.enabled === true && config.blog.hasPublishedPosts === true);

  const linkClass = "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100";
  const headingClass = "font-medium text-gray-700 dark:text-gray-300";

  return (
    <footer className="border-t border-gray-200 py-12 dark:border-gray-800">
      <div className="mx-auto flex max-w-6xl flex-col gap-8 px-4 lg:flex-row lg:justify-between">
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2">
            <Logo />
            <p className="text-sm text-gray-500 dark:text-gray-400">{t("tagline")}</p>
          </div>
          <a href={`mailto:${CONTACT_EMAIL}`} className={`text-sm ${linkClass}`} aria-label={`${t("contact")}: ${CONTACT_EMAIL}`}>
            {CONTACT_EMAIL}
          </a>
          <ul className="flex items-center gap-2" aria-label={t("follow")}>
            {SOCIAL_LINKS.map((link) => (
              <li key={link.network}>
                <a
                  href={link.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label={link.label}
                  title={link.label}
                  className="flex h-8 w-8 items-center justify-center rounded-md border border-gray-200 text-gray-500 hover:border-gray-300 hover:text-gray-900 dark:border-gray-800 dark:text-gray-400 dark:hover:border-gray-700 dark:hover:text-gray-100"
                >
                  <SocialIcon network={link.network} />
                </a>
              </li>
            ))}
          </ul>
        </div>

        <div className="grid grid-cols-2 gap-8 sm:grid-cols-3 sm:gap-12">
          <div className="flex flex-col gap-2 text-sm">
            <span className={headingClass}>{t("product")}</span>
            {PRODUCT_LINKS.map((link) => (
              <Link key={link.href} href={link.href} className={linkClass}>
                {tNav(link.key)}
              </Link>
            ))}
            <a href={CHROME_WEB_STORE_URL} target="_blank" rel="noopener noreferrer" className={linkClass}>
              {t("chromeWebStore")}
            </a>
          </div>

          <div className="flex flex-col gap-2 text-sm">
            <span className={headingClass}>{t("explore")}</span>
            {exploreLinks.map((link) => (
              <Link key={link.key} href={pathFor(link.href, locale)} className={linkClass}>
                {link.key === "about" ? tNav("about") : t(link.key)}
              </Link>
            ))}
          </div>

          <div className="flex flex-col gap-2 text-sm">
            <span className={headingClass}>{t("legal")}</span>
            {LEGAL_LINKS.map((link) => (
              <Link key={link.href} href={link.href} className={linkClass}>
                {t(link.key)}
              </Link>
            ))}
          </div>
        </div>
      </div>

      <p className="mx-auto mt-8 max-w-6xl px-4 text-xs text-gray-500 dark:text-gray-500">
        © {year} e-kariyerim. {t("rights")}
      </p>
    </footer>
  );
}
