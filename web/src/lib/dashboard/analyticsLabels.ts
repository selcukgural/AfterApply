import type { AnalyticsRatesResponse } from "@/types/api";

export interface AnalyticsRateTile {
  key: keyof Pick<
    AnalyticsRatesResponse,
    "responseRate" | "interviewRate" | "offerRate" | "rejectionRate" | "ghostingRate"
  >;
}

export const ANALYTICS_RATE_TILES: AnalyticsRateTile[] = [
  { key: "responseRate" },
  { key: "interviewRate" },
  { key: "offerRate" },
  { key: "rejectionRate" },
  { key: "ghostingRate" },
];
