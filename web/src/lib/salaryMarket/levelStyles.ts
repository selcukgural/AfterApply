import type { SalaryLevel } from "@/types/api";

/**
 * Seniority colours, checked with the palette validator for both themes (2026-09-27): teal /
 * accent blue / amber on white, and a step darker on the dark card (#111827). Amber is not the
 * "warn" status token — a level is an identity, not a state. A plain module, not the chart's:
 * the server-rendered level cards use it too.
 */
export const LEVEL_STYLES: Record<SalaryLevel, { stroke: string; fill: string; swatch: string }> = {
  Junior: { stroke: "stroke-[#15aab7] dark:stroke-[#18a4b0]", fill: "fill-[#15aab7] dark:fill-[#18a4b0]", swatch: "bg-[#15aab7] dark:bg-[#18a4b0]" },
  Middle: { stroke: "stroke-[#2a5fd6] dark:stroke-[#3860d0]", fill: "fill-[#2a5fd6] dark:fill-[#3860d0]", swatch: "bg-[#2a5fd6] dark:bg-[#3860d0]" },
  Senior: { stroke: "stroke-[#e08a00] dark:stroke-[#c98500]", fill: "fill-[#e08a00] dark:fill-[#c98500]", swatch: "bg-[#e08a00] dark:bg-[#c98500]" },
};

export const LEVELS: readonly SalaryLevel[] = ["Junior", "Middle", "Senior"];
