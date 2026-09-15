import { getTranslations } from "next-intl/server";

/**
 * Shown on the two sales-law pages while their wording is a draft. Removed (together with the
 * `draft.*` keys) when the owner's final text is in place — that is the release gate for turning
 * PayTr:Enabled on in production, see DEPLOYMENT.md §15.
 */
export async function LegalDraftNotice() {
  const t = await getTranslations("legalDraft");
  return (
    <p role="note" className="rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-300">
      {t("notice")}
    </p>
  );
}
