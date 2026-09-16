"use client";

import type { ReviewStatementKind } from "@/types/api";

const SELECTED: Record<ReviewStatementKind, string> = {
  Liked: "border-good-ink bg-good-wash text-good-ink dark:border-good-ink",
  Improve: "border-warn-ink bg-warn-wash text-warn-ink dark:border-warn-ink",
};

const IDLE =
  "border-gray-300 bg-white text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800";

/**
 * One catalogue statement as a toggle. The chip shows the short label; the full sentence — the
 * wording that is actually published — is the `title`, which is the tooltip for a mouse and the
 * accessible description for a screen reader. `aria-pressed` rather than a checkbox: it reads as
 * "pressed"/"not pressed", which is what a pick is.
 */
export function StatementChip({
  label,
  sentence,
  kind,
  selected,
  disabled,
  onToggle,
}: {
  label: string;
  sentence: string;
  kind: ReviewStatementKind;
  selected: boolean;
  /** At the cap for this kind: unselected chips stop taking clicks, selected ones still unpick. */
  disabled?: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      title={sentence}
      disabled={disabled && !selected}
      onClick={onToggle}
      className={`rounded-full border px-3 py-1 text-xs font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:cursor-not-allowed disabled:opacity-45 ${
        selected ? SELECTED[kind] : IDLE
      }`}
    >
      {label}
    </button>
  );
}

/** The read-only twin, for cards and the summary panel. */
export function StatementTag({ label, sentence, kind, suffix }: { label: string; sentence: string; kind: ReviewStatementKind; suffix?: string }) {
  return (
    <span title={sentence} className={`inline-flex rounded-full border px-2.5 py-0.5 text-xs font-medium ${SELECTED[kind]}`}>
      {label}
      {suffix ? <span className="ml-1 opacity-80">{suffix}</span> : null}
    </span>
  );
}
