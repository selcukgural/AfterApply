"use client";

import { useId, useState } from "react";
import { useTranslations } from "next-intl";
import type { ReviewStatementKind } from "@/types/api";
import type { Statement } from "@/lib/statements/catalogue";
import { RatingInput } from "@/components/companyReviews/RatingInput";
import { StatementChip } from "@/components/companyReviews/StatementChip";

/**
 * One category of a statement-based form: the label, the stars, and — only once there is a
 * rating — the statements. Three are suggested inline (liked ones for 4–5 stars, "could be
 * better" for 1–3); "N more" opens the category's full list under both headings. Nothing about
 * statements is visible before the row is rated, which is what keeps a form to its star rows
 * at first.
 *
 * Catalogue-agnostic: the caller passes the category's label, its two statement lists and how a
 * key becomes wording, so the review form and the candidate experience form share this row. The
 * chrome strings (required, show more, headings) are the review form's and stay shared.
 */
export function CategoryRatingRow({
  label,
  liked,
  improvable,
  statementText,
  rating,
  required,
  suggestions,
  picked,
  capReached,
  onRate,
  onClear,
  onToggle,
  error,
}: {
  label: string;
  liked: readonly Statement[];
  improvable: readonly Statement[];
  /** The wording behind a key — the short chip label and the full published sentence. */
  statementText: (key: string) => { label: string; sentence: string };
  rating: number;
  required?: boolean;
  suggestions: readonly Statement[];
  /** Every key picked so far, both kinds — the row shows its own category's picks. */
  picked: ReadonlySet<string>;
  capReached: Record<ReviewStatementKind, boolean>;
  onRate: (value: number) => void;
  onClear?: () => void;
  onToggle: (key: string) => void;
  error?: string;
}) {
  const t = useTranslations("companyReviews.form");
  const [open, setOpen] = useState(false);
  const labelId = useId();
  const listId = useId();

  const rated = rating > 0;

  // The inline strip: the suggestions, plus anything from this category already picked that the
  // suggestions do not include — a pick must never vanish from view because the rating changed.
  const inline = rated
    ? [...suggestions, ...[...liked, ...improvable].filter((s) => picked.has(s.key) && !suggestions.some((x) => x.key === s.key))]
    : [];
  const more = liked.length + improvable.length - inline.length;
  const positive = rating >= 4;

  const chip = (statement: Statement) => {
    const text = statementText(statement.key);
    return (
      <StatementChip
        key={statement.key}
        label={text.label}
        sentence={text.sentence}
        kind={statement.kind}
        selected={picked.has(statement.key)}
        disabled={capReached[statement.kind]}
        onToggle={() => onToggle(statement.key)}
      />
    );
  };

  return (
    <div className="flex flex-col gap-2 border-t border-gray-100 py-3 first:border-t-0 dark:border-gray-800">
      <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2">
        <div className="flex items-center gap-2">
          <span id={labelId} className="text-sm font-medium text-gray-700 dark:text-gray-300">
            {label}
          </span>
          {required ? (
            <span className="rounded-full bg-accent-wash px-2 py-0.5 text-[11px] font-medium text-accent-ink">{t("required")}</span>
          ) : null}
        </div>
        <RatingInput label="" labelId={labelId} hideLabel value={rating} onChange={onRate} onClear={onClear} error={error} />
      </div>

      {rated && !open ? (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs text-gray-500 dark:text-gray-400">{positive ? t("suggestedLiked") : t("suggestedImprovable")}</span>
          {inline.map(chip)}
          {more > 0 ? (
            <button
              type="button"
              aria-expanded={false}
              aria-controls={listId}
              onClick={() => setOpen(true)}
              className="px-1.5 py-1 text-xs font-medium text-accent-ink hover:underline"
            >
              {t("showMore", { count: more })}
            </button>
          ) : null}
        </div>
      ) : null}

      {rated && open ? (
        <div id={listId} className="flex flex-col gap-3 rounded-lg border border-gray-100 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-950">
          <div className="flex flex-col gap-1.5">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-good-ink">{t("likedHeading")}</span>
            <div className="flex flex-wrap gap-1.5">{liked.map(chip)}</div>
          </div>
          <div className="flex flex-col gap-1.5">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-warn-ink">{t("improvableHeading")}</span>
            <div className="flex flex-wrap gap-1.5">{improvable.map(chip)}</div>
          </div>
          <button
            type="button"
            aria-expanded={true}
            aria-controls={listId}
            onClick={() => setOpen(false)}
            className="self-start text-xs text-gray-500 underline-offset-2 hover:underline dark:text-gray-400"
          >
            {t("showLess")}
          </button>
        </div>
      ) : null}
    </div>
  );
}
