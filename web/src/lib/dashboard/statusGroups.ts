import type { ApplicationStatus, StatusDistributionItem } from "@/types/api";

/** Semantic colour roles from globals.css. Never used decoratively — each one means a state. */
export type Tone = "accent" | "good" | "warn" | "crit" | "muted";

export const STATUS_TONE: Record<ApplicationStatus, Tone> = {
  Applied: "accent",
  Screening: "accent",
  Interview: "accent",
  TechnicalInterview: "accent",
  FinalInterview: "accent",
  Offer: "good",
  Accepted: "good",
  Rejected: "crit",
  Withdrawn: "muted",
  Ghosted: "muted",
};

export const TONE_FILL: Record<Tone, string> = {
  accent: "bg-accent",
  good: "bg-good",
  warn: "bg-warn",
  crit: "bg-crit",
  muted: "bg-muted",
};

export const TONE_CHIP: Record<Tone, string> = {
  accent: "bg-accent-wash text-accent-ink",
  good: "bg-good-wash text-good-ink",
  warn: "bg-warn-wash text-warn-ink",
  crit: "bg-crit-wash text-crit-ink",
  muted: "bg-muted-wash text-muted-ink",
};

export interface DistributionSplit {
  /**
   * A bucket so much larger than the rest that keeping it on the shared scale would flatten every
   * other bar to nothing. Rendered full-width and out of scale, with the rest re-scaled below it.
   */
  dominant: StatusDistributionItem | null;
  rest: StatusDistributionItem[];
  /** The scale maximum for `rest`. Zero when there is nothing to draw. */
  restMax: number;
}

/**
 * Splits the status distribution so a bulk-import user (1,177 Applied beside single-digit
 * outcomes) still gets a readable chart. When no bucket dominates, everything shares one scale
 * and `dominant` is null.
 */
export function splitDistribution(
  items: readonly StatusDistributionItem[],
  dominanceFactor = 5,
): DistributionSplit {
  const present = items.filter((item) => item.count > 0).sort((a, b) => b.count - a.count);

  if (present.length === 0) {
    return { dominant: null, rest: [], restMax: 0 };
  }

  const [largest, ...rest] = present;
  const restTotal = rest.reduce((sum, item) => sum + item.count, 0);

  if (rest.length > 0 && largest.count > dominanceFactor * restTotal) {
    return { dominant: largest, rest, restMax: rest[0].count };
  }

  return { dominant: null, rest: present, restMax: largest.count };
}

export interface Outcome {
  /** An offer in hand counts as a win even before it is formally accepted. */
  won: number;
  lost: number;
  noAnswer: number;
  withdrawn: number;
  resolved: number;
  /** Share of resolved applications that ended in an offer, 0-100. */
  winRate: number;
}

export function summariseOutcome(items: readonly StatusDistributionItem[]): Outcome {
  const countOf = (status: ApplicationStatus) =>
    items.find((item) => item.status === status)?.count ?? 0;

  const won = countOf("Offer") + countOf("Accepted");
  const lost = countOf("Rejected");
  const noAnswer = countOf("Ghosted");
  const withdrawn = countOf("Withdrawn");
  const resolved = won + lost + noAnswer + withdrawn;

  return {
    won,
    lost,
    noAnswer,
    withdrawn,
    resolved,
    winRate: resolved === 0 ? 0 : (100 * won) / resolved,
  };
}
