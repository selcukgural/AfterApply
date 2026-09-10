import { describe, expect, it } from "vitest";
import type { CvScanFinding, CvScanResponse } from "@/types/api";
import { findingDetails, fixList, inspectScanFile, pointsAtStake, scoreBand } from "./findings";

function finding(overrides: Partial<CvScanFinding> = {}): CvScanFinding {
  return {
    code: "LengthOutOfRange",
    category: "FormatAndLength",
    pointCost: 8,
    evidence: [{ page: 1, quote: "line" }],
    metrics: {},
    ...overrides,
  };
}

function response(findings: CvScanFinding[], score: number): CvScanResponse {
  return {
    score,
    categories: [
      { category: "MachineReadability", weight: 40, score: 40 },
      { category: "SectionsAndDates", weight: 25, score: 25 },
      { category: "Contact", weight: 15, score: 15 },
      { category: "FormatAndLength", weight: 20, score: 20 - (100 - score) },
    ],
    findings,
    document: { format: "Pdf", pageCount: 3, wordCount: 400 },
    extractedTextPreview: "text",
    extractedTextTruncated: false,
  };
}

describe("inspectScanFile", () => {
  it("names the old .doc format specifically", () => {
    // Not "unsupported": .doc is accepted elsewhere in the product, so a reader who picks one
    // needs the export instruction rather than a flat refusal.
    expect(inspectScanFile({ name: "cv.doc", size: 1000 })).toBe("legacyDoc");
  });

  it.each(["cv.pdf", "CV.PDF", "cv.docx"])("accepts %s", (name) => {
    expect(inspectScanFile({ name, size: 1000 })).toBeNull();
  });

  it("refuses anything else and anything oversized", () => {
    expect(inspectScanFile({ name: "cv.pages", size: 1000 })).toBe("unsupportedType");
    expect(inspectScanFile({ name: "cv.pdf", size: 6 * 1024 * 1024 })).toBe("tooLarge");
  });
});

describe("the fix list arithmetic", () => {
  /** The claim the result screen makes in words ("3 fixes, 32 points") has to be a subtraction the
   *  reader can check against the headline. */
  it("adds up to the points missing from the score", () => {
    const result = response(
      [finding({ pointCost: 14 }), finding({ code: "ContactUnreadable", pointCost: 10 }), finding({ pointCost: 8 })],
      68,
    );

    expect(pointsAtStake(result)).toBe(32);
    expect(pointsAtStake(result)).toBe(100 - result.score);
  });

  it("puts the most expensive finding first", () => {
    const result = response([finding({ pointCost: 4 }), finding({ code: "NoTextLayer", pointCost: 40 })], 56);

    expect(fixList(result)[0].code).toBe("NoTextLayer");
  });
});

describe("scoreBand", () => {
  it.each([
    [100, "good"],
    [80, "good"],
    [79, "fair"],
    [55, "fair"],
    [54, "poor"],
    [0, "poor"],
  ])("puts %i in the %s band", (score, band) => {
    expect(scoreBand(score)).toBe(band);
  });
});

describe("findingDetails", () => {
  it("separates a scanned file from a file with one scanned page", () => {
    expect(findingDetails(finding({ code: "NoTextLayer", metrics: { wordCount: 3 } }))).toEqual([
      { key: "noText", args: { wordCount: 3 } },
    ]);

    expect(findingDetails(finding({ code: "NoTextLayer", metrics: { emptyPageCount: 2 } }))).toEqual([
      { key: "emptyPages", args: { count: 2 } },
    ]);
  });

  it("separates unreadable glyphs from letters that were flattened to ASCII", () => {
    expect(
      findingDetails(finding({ code: "BrokenTurkishCharacters", metrics: { unreadableCharacterCount: 12 } })),
    ).toEqual([{ key: "unmapped", args: { count: 12 } }]);

    expect(findingDetails(finding({ code: "BrokenTurkishCharacters", metrics: { turkishMarkerCount: 5 } }))).toEqual([
      { key: "stripped" },
    ]);
  });

  it("lists every missing section and the missing timeline separately", () => {
    const details = findingDetails(
      finding({
        code: "SectionsOrDatesUnreadable",
        metrics: { missingExperience: 1, missingEducation: 0, missingSkills: 1, dateRangeCount: 0 },
      }),
    );

    expect(details.map((detail) => detail.key)).toEqual(["missingExperience", "missingSkills", "noDateRanges"]);
  });

  it("can report a missing e-mail and a header-only phone number at once", () => {
    const details = findingDetails(
      finding({
        code: "ContactUnreadable",
        metrics: { emailFound: 0, phoneFound: 1, onlyInHeaderFooter: 1 },
      }),
    );

    expect(details.map((detail) => detail.key)).toEqual(["noEmail", "onlyInHeaderFooter"]);
  });

  /** A .docx page count is an estimate, and the copy has to say so — an invented number next to an
   *  evidenced finding would undo the point of evidencing anything. */
  it("marks an estimated page count as estimated", () => {
    expect(
      findingDetails(
        finding({ code: "LengthOutOfRange", metrics: { pageCount: 7, wordCount: 3000, pageCountEstimated: 1 } }),
      ),
    ).toEqual([{ key: "longEstimated", args: { pageCount: 7 } }]);
  });
});
