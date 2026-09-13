import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { fetchApprovedReviews, fetchCompanyBySlug } from "@/lib/companies/publicApi.server";
import { ReviewSummaryPanel } from "@/components/companyReviews/ReviewSummaryPanel";
import { CompanyReviewsSection } from "@/components/companyReviews/CompanyReviewsSection";

/**
 * A company's public page: the aggregate and its published reviews, rendered on the server so
 * the reviews are in the HTML. A company nobody has reviewed yet still has a page (the "write a
 * review" call to action needs somewhere to live) but tells crawlers not to index it — a page
 * with a name and nothing else is the thin content that gets a whole site marked down.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/companies/[slug]">): Promise<Metadata> {
  const { locale, slug } = await params;
  const company = await fetchCompanyBySlug(slug, locale);
  if (!company) {
    return {};
  }

  const t = await getTranslations("companies.page");
  return buildMetadata({
    locale,
    path: `/companies/${slug}`,
    title: t("metaTitle", { company: company.name }),
    description: t("metaDescription", { company: company.name, count: company.summary.approvedCount }),
    index: company.summary.approvedCount > 0,
  });
}

export default async function CompanyPage({ params }: PageProps<"/[locale]/companies/[slug]">) {
  const { locale, slug } = await params;
  const [company, reviews] = await Promise.all([fetchCompanyBySlug(slug, locale), fetchApprovedReviews(slug, locale)]);
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
        {company.website && (
          <a
            href={company.website}
            rel="noopener noreferrer nofollow"
            target="_blank"
            className="text-sm text-accent-ink underline-offset-2 hover:underline"
          >
            {t("website")}
          </a>
        )}
      </header>

      <ReviewSummaryPanel summary={company.summary} />

      <CompanyReviewsSection company={company} initialReviews={reviews} />

      <p className="border-t border-gray-200 pt-6 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">{t("disclaimer")}</p>
    </div>
  );
}
