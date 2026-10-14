import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { SITE_URL } from "@/lib/seo/routes";
import { safeExternalUrl } from "@/lib/url/externalLink";
import { ShareRow } from "@/components/share/ShareRow";
import {
  fetchApprovedReviews,
  fetchCandidateExperiences,
  fetchCompanyBySlug,
  fetchCompanyTabFeatures,
} from "@/lib/companies/publicApi.server";
import { companyPageLead, experiencesPageMeta, isCompanyPageIndexable, type RootTranslate } from "@/lib/companies/companyPageContent";
import { ReviewSummaryPanel } from "@/components/companyReviews/ReviewSummaryPanel";
import { CompanyReviewsSection } from "@/components/companyReviews/CompanyReviewsSection";
import { CompanyPageTabs } from "@/components/companies/CompanyPageTabs";
import { CompanySalariesPanel } from "@/components/companySalaries/CompanySalariesPanel";
import { CandidateExperiencesPanel } from "@/components/candidateExperiences/CandidateExperiencesPanel";
import { CompanyIntelligencePanel } from "@/components/companyIntelligence/CompanyIntelligencePanel";
import { SilenceReportCard } from "@/components/companies/SilenceReportCard";

/**
 * A company's public page: the aggregate and its published reviews, rendered on the server so
 * the reviews are in the HTML. A company nobody has reviewed yet still has a page (the "write a
 * review" call to action needs somewhere to live) but tells crawlers not to index it — a page
 * with a name and nothing else is the thin content that gets a whole site marked down. A public
 * candidate experience counts as content for that purpose; salaries, behind sign-in, do not.
 *
 * A company known only for candidate experiences leads with them (2026-10-14): that tab opens
 * first, its entries are fetched here so they are in the HTML, and the title and description say
 * "hiring process experiences" instead of promising reviews the page does not have.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/companies/[slug]">): Promise<Metadata> {
  const { locale, slug } = await params;
  setRequestLocale(locale);
  const company = await fetchCompanyBySlug(slug, locale);
  if (!company) {
    return {};
  }

  const t = await getTranslations("companies.page");
  const tPages = await getTranslations("metadata.pages");
  const leadsWithExperiences = companyPageLead(company) === "experiences";
  const tRoot = await getTranslations();
  // The snippet facts pick their message keys at run time; the catalogue's key types cannot follow.
  const translate: RootTranslate = (key, values) => tRoot(key as never, values as never);
  const experiences = leadsWithExperiences ? await fetchCandidateExperiences(slug, locale) : null;
  const text = leadsWithExperiences
    ? experiencesPageMeta(company, experiences?.items[0] ?? null, translate, locale)
    : {
        title: t("metaTitle", { company: company.name }),
        description: t("metaDescription", { company: company.name, count: company.summary.approvedCount }),
      };
  return buildMetadata({
    locale,
    path: `/companies/${slug}`,
    title: text.title,
    description: text.description,
    index: isCompanyPageIndexable(company),
    kicker: tPages("companies.title"),
  });
}

export default async function CompanyPage({ params }: PageProps<"/[locale]/companies/[slug]">) {
  const { locale, slug } = await params;
  const [company, reviews, experiences, features] = await Promise.all([
    fetchCompanyBySlug(slug, locale),
    fetchApprovedReviews(slug, locale),
    fetchCandidateExperiences(slug, locale),
    fetchCompanyTabFeatures(),
  ]);
  if (!company || !reviews) {
    notFound();
  }

  const t = await getTranslations("companies.page");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-12">
      <nav aria-label={t("breadcrumbLabel")} className="text-sm text-gray-500 dark:text-gray-400">
        <Link href="/companies" className="underline-offset-2 hover:underline">
          {t("allCompanies")}
        </Link>
      </nav>

      <header className="flex flex-col gap-2">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">{company.name}</h1>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        {/* A public page with an Open Graph card is the easiest thing on the site to pass on, and
            the one whose reach (each "X çalışan yorumları" search) the site depends on most. */}
        <ShareRow
          label={t("share")}
          content={{ text: t("shareText", { company: company.name }), url: `${SITE_URL}/${locale}/companies/${slug}` }}
        />
        {/* http(s) only, whatever the column holds: it was read off a third-party page. */}
        {safeExternalUrl(company.website) && (
          <a
            href={safeExternalUrl(company.website)!}
            rel="noopener noreferrer nofollow"
            target="_blank"
            className="text-sm text-accent-ink underline-offset-2 hover:underline"
          >
            {t("website")}
          </a>
        )}
      </header>

      {/* The reviews and experiences halves are rendered here with server data; the tabs decide
          which half is on screen and which one opens first. The salaries half fetches on the
          client, behind sign-in. */}
      <CompanyPageTabs
        companyId={company.id}
        lead={companyPageLead(company)}
        serverFeatures={features}
        reviewCount={company.summary.approvedCount}
        salaryCount={company.salaryCount ?? 0}
        experienceCount={company.candidateExperienceCount ?? 0}
        reviews={
          <>
            <ReviewSummaryPanel summary={company.summary} />
            <CompanyReviewsSection company={company} initialReviews={reviews} />
          </>
        }
        salaries={<CompanySalariesPanel company={company} />}
        experiences={<CandidateExperiencesPanel company={company} initialPage={experiences} />}
        intelligence={<CompanyIntelligencePanel company={company} />}
      />

      {/* Under the tabs, whichever is open: the one thing on the page a stranger can add without
          an account. A client component — the cached page carries none of its state. */}
      <SilenceReportCard companySlug={slug} />

      <p className="border-t border-gray-200 pt-6 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">{t("disclaimer")}</p>
    </div>
  );
}
