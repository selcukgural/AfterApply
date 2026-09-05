import type { AnalyticsRatesResponse } from "@/types/api";

export type FunnelStageKey = "applied" | "responded" | "interview" | "offer";

export interface FunnelStage {
  key: FunnelStageKey;
  count: number;
  /**
   * Share of the *previous* stage that reached this one, 0-100. Null on the first stage.
   *
   * This is what makes the funnel readable at any scale: drawing 1,177 next to 1 on one linear
   * axis leaves the small stages a pixel tall, but "how much of the previous stage got through"
   * is a 0-100 number at every stage, so each rail fills its own full width.
   */
  conversion: number | null;
}

/**
 * Can exceed 100: an application may be recorded straight from Screening to Offer without ever
 * passing through an interview status, so offerCount is not bounded by interviewCount. Callers
 * clamp the drawn bar and show the true figure.
 */
function share(count: number, previous: number): number {
  return previous <= 0 ? 0 : (100 * count) / previous;
}

export function buildFunnel(rates: AnalyticsRatesResponse): FunnelStage[] {
  const counts: { key: FunnelStageKey; count: number }[] = [
    { key: "applied", count: rates.totalApplications },
    { key: "responded", count: rates.respondedCount },
    { key: "interview", count: rates.interviewCount },
    { key: "offer", count: rates.offerCount },
  ];

  return counts.map((stage, index) => ({
    key: stage.key,
    count: stage.count,
    conversion: index === 0 ? null : share(stage.count, counts[index - 1].count),
  }));
}

/**
 * The stage with the steepest drop-off, which is the one worth naming in prose. Only stages that
 * actually had something to lose count, so a funnel that never got off the ground returns null.
 */
export function findBottleneck(stages: FunnelStage[]): FunnelStage | null {
  const candidates = stages.filter((stage, index) => stage.conversion !== null && stages[index - 1].count > 0);
  if (candidates.length === 0) {
    return null;
  }

  return candidates.reduce((worst, stage) => (stage.conversion! < worst.conversion! ? stage : worst));
}
