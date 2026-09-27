import { companyInitials, companyTint } from "@/lib/board/board";

/** Six soft tints, each a wash with its own darker ink, so the letters stay readable in both
 *  themes. Indexed by companyTint: a company keeps its colour on every card and every visit. */
const TINTS = [
  "bg-accent-wash text-accent-ink",
  "bg-good-wash text-good-ink",
  "bg-warn-wash text-warn-ink",
  "bg-muted-wash text-muted-ink",
  "bg-purple-100 text-purple-800 dark:bg-purple-900/40 dark:text-purple-200",
  "bg-pink-100 text-pink-800 dark:bg-pink-900/40 dark:text-pink-200",
];

/**
 * The small square beside a company's name on a board card. Initials on a tint for now; company
 * logos come in a later change (DECISIONS.md 2026-09-27) and will fall back to exactly this.
 * Decorative: the name is always written next to it.
 */
export function CompanyMark({ companyId, companyName, size = "sm" }: { companyId: string; companyName: string; size?: "sm" | "md" }) {
  const box = size === "md" ? "h-8 w-8 rounded-lg text-xs" : "h-5 w-5 rounded-[5px] text-[9px]";
  return (
    <span
      aria-hidden="true"
      className={`inline-flex shrink-0 items-center justify-center font-bold ring-1 ring-inset ring-black/5 dark:ring-white/10 ${box} ${TINTS[companyTint(companyId)]}`}
    >
      {companyInitials(companyName)}
    </span>
  );
}
