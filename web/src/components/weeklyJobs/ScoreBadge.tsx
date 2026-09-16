import { SCORE_BADGE_CLASSES, scoreTone } from "@/lib/weeklyJobs/score";

interface ScoreBadgeProps {
  score: number | null;
  /** The accessible name for an unscored posting ("Henüz puanlanmadı"). */
  unscoredLabel: string;
  /** Prefix for the accessible name of a scored one ("Uyum"). */
  scoreLabel: string;
}

/** The 44px circle on a list row: the number in the tone's wash, or a dash while unscored. */
export function ScoreBadge({ score, unscoredLabel, scoreLabel }: ScoreBadgeProps) {
  const tone = scoreTone(score);
  return (
    <span
      role="img"
      aria-label={score === null ? unscoredLabel : `${scoreLabel} ${score}`}
      className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-sm font-semibold ${SCORE_BADGE_CLASSES[tone]}`}
    >
      {score === null ? "—" : score}
    </span>
  );
}
