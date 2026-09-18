import { useTranslations } from "next-intl";
import type { ContributionKind } from "@/types/api";

const STYLE: Record<ContributionKind, string> = {
  Review: "bg-accent-wash text-accent-ink",
  Salary: "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300",
  Experience: "bg-amber-50 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300",
};

const KEY: Record<ContributionKind, "review" | "salary" | "experience"> = {
  Review: "review",
  Salary: "salary",
  Experience: "experience",
};

/** Which of the three kinds a card on the merged "my contributions" list is. */
export function ContributionKindBadge({ kind }: { kind: ContributionKind }) {
  const t = useTranslations("companyReviews.mine.kind");
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold tracking-wide ${STYLE[kind]}`}>
      {t(KEY[kind])}
    </span>
  );
}
