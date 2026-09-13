import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { CompanyDirectory } from "@/components/companyReviews/CompanyDirectory";
import { LandingIcon, type LandingIcon as LandingIconName } from "@/components/landing/landingIcons";

/** What a first-time visitor needs before the search box: what a review holds, and the two promises. */
const EXPLAINER: readonly { key: "what" | "anonymous" | "moderated"; icon: LandingIconName }[] = [
  { key: "what", icon: "tracking" },
  { key: "anonymous", icon: "noResponse" },
  { key: "moderated", icon: "check" },
];

/**
 * The public company directory: every company with at least one published review, and the door
 * to writing one. Shared segment in both locales (/tr/companies, /en/companies), like /benchmark —
 * see that page for why the path is not translated.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/companies">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/companies", "companies");
}

export default async function CompaniesPage() {
  const t = await getTranslations("companies.directory");
  const tScoring = await getTranslations("companies.scoring");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-12">
      <header className="flex flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-[60ch] text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      {/* Three cards instead of the one-line "reviews never show who wrote them" that stood here
          until 2026-09-13: a stranger arriving from the landing page or a search needs to know what
          a review holds and on what terms before the search box makes sense. The scoring link moves
          into the moderation card, the one place a reader wonders how the number is made. */}
      <div className="grid gap-4 sm:grid-cols-3">
        {EXPLAINER.map((item) => (
          <div key={item.key} className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
            <div className="flex items-center gap-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-accent-wash text-accent-ink">
                <LandingIcon name={item.icon} />
              </span>
              <span className="font-semibold text-gray-900 dark:text-gray-100">{t(`explainer.${item.key}.title`)}</span>
            </div>
            <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t(`explainer.${item.key}.body`)}</p>
            {item.key === "moderated" ? (
              <Link href="/companies/scoring" className="text-sm text-accent-ink underline-offset-2 hover:underline">
                {tScoring("linkLabel")}
              </Link>
            ) : null}
          </div>
        ))}
      </div>

      <CompanyDirectory />
    </div>
  );
}
