"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { useClientConfig } from "@/hooks/useClientConfig";
import { ProfileIdentityCard } from "@/components/profile/ProfileIdentityCard";
import { PlanCard } from "@/components/profile/PlanCard";
import { ContributionsCard } from "@/components/profile/ContributionsCard";
import { ActivityTiles } from "@/components/profile/ActivityTiles";
import { BreakCard } from "@/components/profile/BreakCard";

/**
 * Who the account is and what it holds: the name (editable), the e-mail (not), the member-since
 * date, the plan, the newest contributions and three activity counts. Chosen as variant A on the
 * 2026-09-17 canvas: "who I am" gets its own calm page, and /settings keeps the technical and
 * the dangerous — the extension key, the data export, the account deletion — which the last
 * line points at.
 */
export default function ProfilePage() {
  const t = useTranslations("profile");
  const { user } = useAuth();
  const { config } = useClientConfig();

  if (!user) return null;

  const reviewsOn = config.companyReviews?.enabled === true;
  const salariesOn = config.companySalaries?.enabled === true;
  const experiencesOn = config.candidateExperiences?.enabled === true;

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      <ProfileIdentityCard key={user.id} user={user} />
      <PlanCard />
      {reviewsOn && <ContributionsCard showSalaries={salariesOn} showExperiences={experiencesOn} />}
      <ActivityTiles />
      <BreakCard />

      <p className="text-sm text-gray-500 dark:text-gray-400">
        {t.rich("settingsHint", {
          link: (chunks) => (
            <Link href="/settings" className="text-accent-ink underline-offset-2 hover:underline">
              {chunks}
            </Link>
          ),
        })}
      </p>
    </div>
  );
}
