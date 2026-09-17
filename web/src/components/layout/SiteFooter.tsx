import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { Logo } from "@/components/layout/Logo";
import { CHROME_WEB_STORE_URL } from "@/lib/constants/chromeWebStore";

/** The landing page's sections — the anchors resolve from any page — and the store listing. */
const PRODUCT_LINKS = [
  { href: "/#how-it-works", key: "howItWorks" },
  { href: "/#extension", key: "extension" },
  { href: "/#features", key: "features" },
  { href: "/#mission", key: "mission" },
] as const;

/** Every public page a visitor can browse, under the same names the header uses. */
const EXPLORE_LINKS = [
  { href: "/companies", key: "companies" },
  { href: "/cv-tarama", key: "cvScan" },
  { href: "/benchmark", key: "benchmark" },
  { href: "/guide", key: "guide" },
  { href: "/help", key: "help" },
] as const;

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
 */
export async function SiteFooter() {
  const t = await getTranslations("landing.footer");
  const tNav = await getTranslations("siteNav");
  const year = new Date().getFullYear();

  const linkClass = "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100";
  const headingClass = "font-medium text-gray-700 dark:text-gray-300";

  return (
    <footer className="border-t border-gray-200 py-12 dark:border-gray-800">
      <div className="mx-auto flex max-w-6xl flex-col gap-8 px-4 lg:flex-row lg:justify-between">
        <div className="flex flex-col gap-2">
          <Logo />
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("tagline")}</p>
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
            {EXPLORE_LINKS.map((link) => (
              <Link key={link.href} href={link.href} className={linkClass}>
                {t(link.key)}
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

      <p className="mx-auto mt-8 max-w-6xl px-4 text-xs text-gray-400 dark:text-gray-600">
        © {year} e-kariyerim. {t("rights")}
      </p>
    </footer>
  );
}
