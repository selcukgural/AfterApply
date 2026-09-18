"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useExperienceInvite } from "@/hooks/useExperienceInvite";
import { closingMoment } from "@/lib/applications/shareExperience";
import type { ApplicationStatus } from "@/types/api";

interface AcceptedClosingNoteProps {
  status: ApplicationStatus;
  companyId: string;
  companyName: string;
  companySlug: string | null | undefined;
}

/**
 * The card an application carries once its offer is accepted (DEVELOPMENT_PLAN.md, T-series,
 * T7): a congratulation, one last offer to put the process on record for the next candidate,
 * and the reminder that the data is theirs to download whenever they like while the account
 * waits. Job searching is episodic — this person will search again in a couple of years, and
 * the place they come back to should be the one that let them leave well.
 *
 * Nothing here points at account deletion, on purpose. The experience line disappears once they
 * have written one; the rest stays, since it is the application's closing line, not a prompt.
 * Same tone rules as the rest of the series: no exclamation mark, no accent colour.
 */
export function AcceptedClosingNote({ status, companyId, companyName, companySlug }: AcceptedClosingNoteProps) {
  const t = useTranslations("applications.detail.accepted");
  const accepted = closingMoment(status) === "accepted";
  const invite = useExperienceInvite(companyId, companySlug, accepted);

  if (!accepted) return null;

  return (
    <section className="flex flex-col gap-2 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      {invite && (
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("share.text")}{" "}
          <Link
            href={`/contribute?tab=experience&company=${encodeURIComponent(companySlug!)}`}
            className="font-medium text-gray-900 hover:underline dark:text-gray-100"
          >
            {t("share.link", { company: companyName })}
          </Link>
        </p>
      )}
      <p className="text-sm text-gray-600 dark:text-gray-400">
        {t("keep.text")}{" "}
        <Link href="/settings#export" className="font-medium text-gray-900 hover:underline dark:text-gray-100">
          {t("keep.link")}
        </Link>
      </p>
    </section>
  );
}
