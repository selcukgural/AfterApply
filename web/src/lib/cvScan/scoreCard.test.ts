import { describe, expect, it } from "vitest";
import { SCORE_CARD_CATEGORIES, formatScoreCard, parseScoreCard } from "./scoreCard";

const categories = [
  { category: "MachineReadability" as const, weight: 40, score: 34 },
  { category: "SectionsAndDates" as const, weight: 25, score: 22 },
  { category: "Contact" as const, weight: 15, score: 13 },
  { category: "FormatAndLength" as const, weight: 20, score: 19 },
];

describe("the score card", () => {
  it("weights the four categories the way the API does — they sum to 100", () => {
    expect(SCORE_CARD_CATEGORIES.reduce((sum, entry) => sum + entry.weight, 0)).toBe(100);
  });

  it("writes the score and the subtotals in report order, whatever order they arrived in", () => {
    expect(formatScoreCard(88, categories)).toBe("88-34-22-13-19");
    expect(formatScoreCard(88, [...categories].reverse())).toBe("88-34-22-13-19");
  });

  it("reads its own output back", () => {
    expect(parseScoreCard("88-34-22-13-19")).toEqual({ score: 88, categories });
  });

  it("accepts a score alone", () => {
    expect(parseScoreCard("88")).toEqual({ score: 88, categories: null });
    expect(parseScoreCard("0")).toEqual({ score: 0, categories: null });
    expect(parseScoreCard("100")).toEqual({ score: 100, categories: null });
  });

  it("refuses a card the scan could not have produced", () => {
    // Over 100, a subtotal over its weight, parts that do not add up, a missing part.
    expect(parseScoreCard("101")).toBeNull();
    expect(parseScoreCard("88-41-22-13-12")).toBeNull();
    expect(parseScoreCard("88-34-22-13-18")).toBeNull();
    expect(parseScoreCard("88-34-22-13")).toBeNull();
  });

  it("refuses anything that is not numbers", () => {
    for (const segment of ["", "abc", "88-", "-88", "88-34-22-13-19-1", "8.8", "88 ", "١٢"]) {
      expect(parseScoreCard(segment)).toBeNull();
    }
  });
});

describe("the card's weights are the API's", () => {
  // Read from the C# source rather than duplicated as a fixture: the day someone rebalances the
  // score, this is what says the share card has to follow.
  it("mirrors CvScanScoring.Weights category by category", async () => {
    const { readFileSync } = await import("node:fs");
    const { fileURLToPath } = await import("node:url");
    const path = await import("node:path");
    const source = readFileSync(
      path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../../../src/AfterApply.Application/CvScan/CvScanScoring.cs"),
      "utf8",
    );
    for (const { category, weight } of SCORE_CARD_CATEGORIES) {
      expect(source, `${category} weight`).toMatch(new RegExp(`\\[CvScanCategory\\.${category}\\]\\s*=\\s*${weight}\\b`));
    }
  });
});
