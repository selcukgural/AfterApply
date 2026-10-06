"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";
import { SocialSignIn } from "@/components/auth/SocialSignIn";
import { HeroCvDropzone } from "@/components/landing/HeroCvDropzone";

/**
 * The hero's right-hand side since 2026-10-02 (landing canvas option B): the sign-up itself, not a
 * link to it.
 *
 * The month before, 556 landing views led to 55 views of /register, and about 40% of those became
 * accounts — most of them through LinkedIn, Google or GitHub. The loss was the step between the
 * two pages, not the sign-up page, so the providers moved onto the first screen. Consent is not
 * asked here: a new social account still passes the "complete your sign-up" form, which has the
 * consent box, and the e-mail route ends on /register, which has it too.
 *
 * A signed-in visitor has no use for a sign-up card, so they keep the CV drop zone that stood here
 * before. The drop zone is also still the band variant's right-hand side (HeroSection).
 */
export function HeroSignUpCard() {
  const { isAuthenticated } = useAuth();
  const t = useTranslations("landing.hero.signUp");
  const tRegister = useTranslations("auth.register");
  const tBenefits = useTranslations("auth.register.benefits");

  if (isAuthenticated) {
    return <HeroCvDropzone />;
  }

  return (
    <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{tRegister("title")}</h2>
      <ul className="mt-3 mb-5 flex flex-col gap-1.5 text-sm text-gray-600 dark:text-gray-400">
        {(["item1", "item2", "item3"] as const).map((key) => (
          <li key={key} className="flex gap-2">
            <span aria-hidden="true" className="text-blue-600 dark:text-blue-400">✓</span>
            <span>{tBenefits(key)}</span>
          </li>
        ))}
      </ul>

      <SocialSignIn onStart={() => trackSiteTraffic("cta_hero_social_sign_in")} />

      <div className="flex flex-col gap-2 text-sm">
        {/* The same event as the page's other "get started" buttons: it is one, just quieter. */}
        <Link
          href="/register"
          onClick={() => trackSiteTraffic("cta_get_started")}
          className="font-medium text-blue-600 hover:underline dark:text-blue-400"
        >
          {t("email")}
        </Link>
        <p className="text-gray-600 dark:text-gray-400">
          {t("haveAccount")}{" "}
          <Link href="/login" className="text-blue-600 underline underline-offset-2 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300">
            {t("signIn")}
          </Link>
        </p>
      </div>
    </div>
  );
}
