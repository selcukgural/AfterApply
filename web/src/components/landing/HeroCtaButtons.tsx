"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";

/**
 * The hero's calls to action, now that the scan is the primary one.
 *
 * Separate from `CtaButtons` rather than another prop on it, for two reasons that pull in the same
 * direction: `FinalCtaSection` still wants registration as its primary — it is the bottom of the
 * page, where someone who read all of it has already had their answer — and `CtaButtons`' signed-in
 * branch renders *only* "go to dashboard", which would delete the scan button for anyone with an
 * account. The scan is useful to them too; it is the one thing here that does not care who you are.
 *
 * The primary is a plain link to /cv-tarama rather than a button that opens the file picker. It
 * therefore works before hydration and without JavaScript, which makes the drop zone beside it a
 * pure enhancement rather than the only way in.
 */
export function HeroCtaButtons() {
  const t = useTranslations("landing.hero");
  const tNav = useTranslations("siteNav");
  const { isAuthenticated } = useAuth();

  return (
    <div className="flex flex-col items-start gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <Link href="/cv-tarama" className={buttonClassName("primary", "px-6 py-3 text-base")}>
          {t("ctaPrimary")}
        </Link>

        {isAuthenticated ? (
          <Link href="/dashboard" className={buttonClassName("secondary", "px-6 py-3 text-base")}>
            {tNav("goToDashboard")}
          </Link>
        ) : (
          // Still the same event on the same button, so "clicked the register CTA" stays
          // comparable across this change rather than restarting as a new number.
          <Link
            href="/register"
            className={buttonClassName("secondary", "px-6 py-3 text-base")}
            onClick={() => trackSiteTraffic("cta_get_started")}
          >
            {t("ctaSecondary")}
          </Link>
        )}
      </div>

      {/* Demoted from a button to a line of text: it points at a section of this same page, and the
          navbar carries it too. Nothing is lost and the eye has one less thing to rank. */}
      <a
        href="#how-it-works"
        className="text-sm text-gray-600 underline underline-offset-2 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
      >
        {t("ctaHowItWorks")}
      </a>
    </div>
  );
}
