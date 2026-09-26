import { getLocale, getTranslations } from "next-intl/server";
import { cvScanPath } from "@/lib/cvScan/path";
import { Link } from "@/i18n/navigation";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { GUIDE_PATH } from "@/lib/guide/guideLinks";

/**
 * The 404 page, in the visitor's language and inside the site's chrome. Reached through
 * `[locale]/[...rest]/page.tsx` for unknown URLs and through any `notFound()` under the locale
 * segment (an unknown company slug, a guide article that does not exist in this language).
 *
 * The four links are the site's front doors, not a sitemap: someone who typed a wrong address or
 * followed a stale link is most likely after one of the tools or the guide.
 */
export default async function NotFoundPage() {
  const locale = await getLocale();
  const t = await getTranslations({ locale, namespace: "notFound" });

  const doors = [
    { href: "/", label: t("home") },
    { href: cvScanPath(locale), label: t("cvScan") },
    { href: "/companies", label: t("companies") },
    { href: GUIDE_PATH, label: t("guide") },
    { href: "/help", label: t("help") },
  ] as const;

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <main className="flex flex-1 items-center justify-center px-4 py-20">
        <div className="flex w-full max-w-lg flex-col items-start gap-6">
          <span className="font-mono text-sm text-gray-500 dark:text-gray-500">404</span>
          <h1 className="text-3xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{t("title")}</h1>
          <p className="text-gray-600 dark:text-gray-400">{t("body")}</p>
          <ul className="flex flex-wrap gap-2">
            {doors.map((door) => (
              <li key={door.href}>
                <Link
                  href={door.href}
                  className="inline-block rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-800 hover:border-gray-500 dark:border-gray-700 dark:text-gray-200 dark:hover:border-gray-500"
                >
                  {door.label}
                </Link>
              </li>
            ))}
          </ul>
        </div>
      </main>
      <SiteFooter />
    </div>
  );
}
