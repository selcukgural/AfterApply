import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { Logo } from "@/components/layout/Logo";

const PRODUCT_LINKS = [
  { href: "/#how-it-works", key: "howItWorks" },
  { href: "/#extension", key: "extension" },
  { href: "/#features", key: "features" },
  { href: "/companies", key: "companies" },
  { href: "/#mission", key: "mission" },
] as const;

const RESOURCE_LINKS = [
  { href: "/cv-tarama", key: "cvScan" },
  { href: "/benchmark", key: "benchmark" },
  { href: "/guide", key: "guide" },
  { href: "/help", key: "help" },
  { href: "/privacy", key: "privacy" },
  { href: "/extension-privacy", key: "extensionPrivacy" },
  { href: "/cookies", key: "cookies" },
] as const;

/**
 * The footer every signed-out surface shares — the landing page and every page under `(public)`,
 * which had none before 2026-09-13. Product links go through next-intl's Link with a leading `/`,
 * so the landing's section anchors resolve from any page, not only from the landing itself.
 */
export async function SiteFooter() {
  const t = await getTranslations("landing.footer");
  const tNav = await getTranslations("siteNav");
  const year = new Date().getFullYear();

  const linkClass = "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100";

  return (
    <footer className="border-t border-gray-200 py-12 dark:border-gray-800">
      <div className="mx-auto flex max-w-6xl flex-col gap-8 px-4 sm:flex-row sm:justify-between">
        <div className="flex flex-col gap-2">
          <Logo />
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("tagline")}</p>
        </div>

        <div className="flex gap-12">
          <div className="flex flex-col gap-2 text-sm">
            <span className="font-medium text-gray-700 dark:text-gray-300">{t("product")}</span>
            {PRODUCT_LINKS.map((link) => (
              <Link key={link.href} href={link.href} className={linkClass}>
                {tNav(link.key)}
              </Link>
            ))}
          </div>

          <div className="flex flex-col gap-2 text-sm">
            <span className="font-medium text-gray-700 dark:text-gray-300">{t("resources")}</span>
            {RESOURCE_LINKS.map((link) => (
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
