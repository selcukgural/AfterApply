import type { AnalyticsRatesResponse } from "@/types/api";

export interface AnalyticsRateTile {
  key: keyof Pick<
    AnalyticsRatesResponse,
    "responseRate" | "interviewRate" | "offerRate" | "rejectionRate" | "ghostingRate"
  >;
  label: string;
}

export const ANALYTICS_RATE_TILES: AnalyticsRateTile[] = [
  { key: "responseRate", label: "Yanıt Oranı" },
  { key: "interviewRate", label: "Mülakat Oranı" },
  { key: "offerRate", label: "Teklif Oranı" },
  { key: "rejectionRate", label: "Red Oranı" },
  { key: "ghostingRate", label: "Kayboldu Oranı" },
];
