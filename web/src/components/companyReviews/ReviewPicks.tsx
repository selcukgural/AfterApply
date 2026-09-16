"use client";

import { useTranslations } from "next-intl";
import { findStatement } from "@/lib/companyReviews/statementCatalogue";
import { StatementTag } from "@/components/companyReviews/StatementChip";

/** The two pick lists of a review, read-only, for the author's list and the admin detail. Renders
 *  nothing when there are no picks — a review made of ratings alone is complete as it is. */
export function ReviewPicks({ liked, improvable }: { liked: string[]; improvable: string[] }) {
  const t = useTranslations("companyReviews.card");
  const tStatements = useTranslations("companyReviews.statements");

  if (liked.length === 0 && improvable.length === 0) return null;

  const tags = (keys: string[]) =>
    keys.flatMap((key) => {
      const statement = findStatement(key);
      if (!statement) return [];
      return [
        <li key={key}>
          <StatementTag label={tStatements(`${key}.label`)} sentence={tStatements(`${key}.sentence`)} kind={statement.kind} />
        </li>,
      ];
    });

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {liked.length > 0 ? (
        <section>
          <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-good-ink">{t("liked")}</h4>
          <ul className="flex flex-wrap gap-1.5">{tags(liked)}</ul>
        </section>
      ) : null}
      {improvable.length > 0 ? (
        <section>
          <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-warn-ink">{t("improvable")}</h4>
          <ul className="flex flex-wrap gap-1.5">{tags(improvable)}</ul>
        </section>
      ) : null}
    </div>
  );
}
