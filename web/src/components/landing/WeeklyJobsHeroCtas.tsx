"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";

/**
 * The weekly-postings hero's calls to action. The primary is registration (the postings page
 * itself needs an account; the plan is bought from inside it), the same `cta_get_started` event
 * as every other register button on this page so the funnel keeps one number. "How it works"
 * goes to the help topic, where the price and the refund terms are — a visitor deciding whether
 * to sign up reads those there, not in a hero.
 */
export function WeeklyJobsHeroCtas() {
  const t = useTranslations("landing.weeklyHero");
  const tNav = useTranslations("siteNav");
  const { isAuthenticated } = useAuth();

  return (
    <div className="flex flex-wrap items-center gap-3">
      {isAuthenticated ? (
        <Link href="/weekly-jobs" className={buttonClassName("primary", "px-6 py-3 text-base")}>
          {t("ctaSignedIn")}
        </Link>
      ) : (
        <Link
          href="/register"
          className={buttonClassName("primary", "px-6 py-3 text-base")}
          onClick={() => trackSiteTraffic("cta_get_started")}
        >
          {tNav("getStarted")}
        </Link>
      )}
      <Link href="/help/weekly-jobs" className={buttonClassName("secondary", "px-6 py-3 text-base")}>
        {t("ctaHow")}
      </Link>
    </div>
  );
}
