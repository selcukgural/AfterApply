import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { ABOUT_PATHS } from "@/lib/about/path";
import { CONTACT_EMAIL, SOCIAL_LINKS } from "@/lib/constants/socialLinks";
import { SocialIcon } from "@/components/layout/SocialIcon";
import { SiteStatsStrip } from "@/components/landing/SiteStatsStrip";

/**
 * Who builds this and why — `/tr/hakkimizda`, `/en/about` by rewrite (`lib/about/path.ts`). Home
 * of what used to be three landing-page sections (Vision, Mission, "Today and tomorrow"; moved
 * 2026-09-18, growth audit finding 11), rewritten in the product's own voice: a team, the problem
 * it sees on the candidate's side, what it wants to do, what exists, the principles, and how to
 * reach it. No person is named, on purpose.
 *
 * One column, read top to bottom, like a letter: a search engine's "who is behind this" signal
 * is a real reason written by real people, not a company boilerplate box.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/hakkimizda">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, ABOUT_PATHS, "about");
}

export default async function AboutPage({ params }: PageProps<"/[locale]/hakkimizda">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("about");

  const todayItems = ["tracking", "import", "reminders", "cv", "tools", "companies"] as const;
  const principleItems = ["time", "data", "transparency"] as const;

  const heading = "text-base font-semibold text-gray-900 dark:text-gray-100";
  const body = "text-sm leading-6 text-gray-600 dark:text-gray-400";

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <span className="text-xs font-semibold uppercase tracking-wider text-accent-ink">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-[62ch] text-base leading-7 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </header>

      <section className="flex flex-col gap-3">
        <h2 className={heading}>{t("problem.title")}</h2>
        <div className="flex flex-col gap-1">
          <p className={body}>{t("problem.story1")}</p>
          <p className={body}>{t("problem.story2")}</p>
          <p className="text-sm font-medium leading-6 text-gray-900 dark:text-gray-100">{t("problem.story3")}</p>
        </div>
        <p className={body}>{t("problem.body")}</p>
        <p className="text-sm leading-6 text-gray-900 dark:text-gray-100">
          <span className="font-semibold">{t("problem.closing")}</span> {t("problem.closingNote")}
        </p>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className={heading}>{t("aim.title")}</h2>
        <p className={body}>{t("aim.body")}</p>
      </section>

      {/* The same figures as the landing page's strip, under their own heading; nothing while
          every one of them is under the API's threshold. */}
      <SiteStatsStrip heading />

      <section className="flex flex-col gap-3">
        <h2 className={heading}>{t("today.title")}</h2>
        <ul className="grid list-disc gap-x-8 gap-y-1.5 pl-5 text-sm text-gray-600 sm:grid-cols-2 dark:text-gray-400">
          {todayItems.map((item) => (
            <li key={item}>{t(`today.items.${item}`)}</li>
          ))}
        </ul>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className={heading}>{t("principles.title")}</h2>
        <ul className="flex list-disc flex-col gap-1.5 pl-5 text-sm text-gray-600 dark:text-gray-400">
          {principleItems.map((item) => (
            <li key={item}>{t(`principles.items.${item}`)}</li>
          ))}
          <li>
            {t("principles.items.privacy")}{" "}
            <Link href="/privacy" className="font-medium text-gray-900 underline underline-offset-2 dark:text-gray-100">
              {t("principles.privacyLink")}
            </Link>
          </li>
        </ul>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className={heading}>{t("contact.title")}</h2>
        <p className={body}>
          {t("contact.body")}{" "}
          <a href={`mailto:${CONTACT_EMAIL}`} className="font-medium text-gray-900 underline underline-offset-2 dark:text-gray-100">
            {CONTACT_EMAIL}
          </a>
        </p>
        <ul className="flex flex-wrap items-center gap-2">
          {SOCIAL_LINKS.map((link) => (
            <li key={link.network}>
              <a
                href={link.href}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-2 rounded-md border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 hover:border-gray-300 hover:text-gray-900 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-600 dark:hover:text-gray-100"
              >
                <SocialIcon network={link.network} />
                {link.label}
              </a>
            </li>
          ))}
        </ul>
        <p className={body}>{t("contact.feedback")}</p>
      </section>
    </div>
  );
}
