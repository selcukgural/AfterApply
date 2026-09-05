import { Card } from "@/components/dashboard/Card";
import { TONE_CHIP, TONE_FILL, type Tone } from "@/lib/dashboard/statusGroups";

/**
 * A secondary metric. `value` arrives pre-formatted so the tile stays usable from server
 * components (the landing page renders it too) without pulling a locale hook in.
 *
 * Deliberately not the hero: the number is 30px, not 56px. Exactly one figure per view gets to
 * be the biggest one — see HeroTile.
 */
export function StatTile({
  label,
  value,
  chip,
  tone = "muted",
  size = "md",
}: {
  label: string;
  value: string;
  chip?: string;
  tone?: Tone;
  /** "sm" is for the landing page's cramped 3-across preview, not for the dashboard itself. */
  size?: "sm" | "md";
}) {
  return (
    <Card className={`flex flex-col gap-1 ${size === "sm" ? "p-3" : ""}`}>
      <span className="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
        <span aria-hidden className={`size-1.5 shrink-0 rounded-full ${TONE_FILL[tone]}`} />
        <span className="truncate">{label}</span>
      </span>
      <span
        className={`font-semibold tracking-tight text-gray-900 dark:text-gray-100 ${size === "sm" ? "text-xl" : "text-3xl"}`}
      >
        {value}
      </span>
      {chip ? (
        <span className={`mt-1 w-fit rounded-full px-2 py-0.5 text-xs font-medium ${TONE_CHIP[tone]}`}>{chip}</span>
      ) : null}
    </Card>
  );
}
