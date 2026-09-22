import { createTranslator } from "next-intl";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

// The answer counts on the benchmark result read "1 answers" in English until 2026-09-22: the
// figure was passed as a preformatted string, so the catalogue had no number to pluralise on.
// Every such message now takes a raw `n` beside the formatted figure; English pluralises on it.

const enT = createTranslator({ locale: "en", messages: en, namespace: "benchmark" });
const trT = createTranslator({ locale: "tr", messages: tr, namespace: "benchmark" });

describe("benchmark answer counts", () => {
  it.each([
    ["result.comparedTitle", { sector: "Education", sampleSize: "1", n: 1 }, "1 answer in Education"],
    ["result.overallTitle", { sector: "Education", sampleSize: "1", minimum: "30", n: 1 }, "Education has only 1 answer so far — a median needs 30."],
    ["result.overallNote", { count: "1", n: 1 }, "based on 1 answer · not specific to your field"],
    ["survey.answers", { count: "1", n: 1 }, "1 answer"],
    ["participation", { count: "1", n: 1 }, "Based on 1 answer."],
  ] as const)("says %s in the singular for one answer", (key, values, expected) => {
    expect(enT(key, values)).toBe(expected);
  });

  it("uses the plural, with grouping, for more than one", () => {
    expect(enT("result.overallNote", { count: "1,204", n: 1204 })).toBe("based on 1,204 answers · not specific to your field");
    expect(enT("result.overallTitle", { sector: "Education", sampleSize: "4", minimum: "30", n: 4 })).toBe(
      "Education has only 4 answers so far — a median needs 30.",
    );
  });

  it("leaves Turkish on the formatted figure, which has no plural to get wrong", () => {
    expect(trT("survey.answers", { count: "1.204", n: 1204 })).toBe("1.204 cevap");
  });
});
