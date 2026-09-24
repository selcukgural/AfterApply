"use client";

import { useTranslations } from "next-intl";

/**
 * The "backed by a tracked application" label (2026-09-24, research item #7). An experience is
 * backed by any application its author tracked at the company; a salary or a review only by one
 * that ended Accepted — having applied does not show someone worked there. The rule (and its
 * 14 days, which the copy repeats) lives in ContributionProofQueries on the API; this file only
 * draws the yes/no the record carries. The wording never says "verified": the content is not.
 */
export type ProofKind = "tracked" | "accepted";

function CheckIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" aria-hidden="true">
      <path d="M5 12l5 5 9-10" />
    </svg>
  );
}

function InfoIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8v.01" />
    </svg>
  );
}

/** The chip on a card. */
export function ProofChip({ kind }: { kind: ProofKind }) {
  const t = useTranslations("contributionProof");
  return (
    <span
      title={t(`title.${kind}`)}
      className="inline-flex h-[22px] items-center gap-1 whitespace-nowrap rounded-full bg-accent-wash px-2 text-xs font-medium text-accent-ink ring-1 ring-inset ring-accent/20"
    >
      <CheckIcon />
      {t(`chip.${kind}`)}
    </span>
  );
}

/** The one line under a list that says what the chip means — drawn only when a chip is on screen. */
export function ProofNote({ kind }: { kind: ProofKind }) {
  const t = useTranslations("contributionProof");
  return (
    <p className="flex items-start gap-1.5 text-[13px] leading-relaxed text-gray-500 dark:text-gray-400">
      <span className="pt-0.5 text-accent-ink">
        <InfoIcon />
      </span>
      <span>
        {t.rich(`note.${kind}`, {
          strong: (chunks) => <strong className="font-medium text-gray-700 dark:text-gray-300">{chunks}</strong>,
        })}
      </span>
    </p>
  );
}

/** The author's own card: whether the company page shows the chip on this row, and if not, why. */
export function MyProofLine({ kind, backed }: { kind: ProofKind; backed: boolean }) {
  const t = useTranslations("contributionProof");
  if (backed) {
    return (
      <div className="flex flex-wrap items-center gap-2">
        <ProofChip kind={kind} />
        <span className="text-[13px] text-gray-600 dark:text-gray-400">{t("mine.on")}</span>
      </div>
    );
  }
  return <p className="text-[13px] leading-relaxed text-gray-500 dark:text-gray-400">{t(`mine.off.${kind}`)}</p>;
}
