import type { CvScanFinding, CvScanResponse } from "@/types/api";
import { MAX_CV_FILE_SIZE_BYTES } from "@/lib/cv/cvFile";

/**
 * Everything the result screen needs to say about a finding, kept out of the components so it can
 * be tested as arithmetic and string keys rather than through a rendered tree.
 *
 * The rule this file exists to keep: **the page never invents a number.** Every figure it shows
 * comes off the response — the score, the category weights, the cost of each finding — because the
 * whole pitch of the scan is that a reader can add it up themselves.
 */

/** .doc is missing on purpose: the upload rules accept it elsewhere in the product, but there is
 *  no managed reader for the old OLE2 format here and shelling out to a converter would hand an
 *  anonymous stranger's file to another process. The server says so in as many words; refusing it
 *  in the picker saves the round trip. */
export const CV_SCAN_ACCEPT = ".pdf,.docx";

export type CvScanFileProblem = "unsupportedType" | "legacyDoc" | "tooLarge";

export function inspectScanFile(file: { name: string; size: number }): CvScanFileProblem | null {
  const name = file.name.trim().toLowerCase();

  if (name.endsWith(".doc")) {
    return "legacyDoc";
  }

  if (!name.endsWith(".pdf") && !name.endsWith(".docx")) {
    return "unsupportedType";
  }

  return file.size > MAX_CV_FILE_SIZE_BYTES ? "tooLarge" : null;
}

/** The findings a reader can actually act on, worst first. A finding that cost nothing — its
 *  category had already run out of points — is still shown, but below the ones that did. */
export function fixList(result: CvScanResponse): CvScanFinding[] {
  return [...result.findings].sort((a, b) => b.pointCost - a.pointCost);
}

/** What the fix list adds up to. Equal to 100 minus the score, by construction: the server charges
 *  each finding out of its category's remaining weight, so the costs shown are the costs paid. */
export function pointsAtStake(result: CvScanResponse): number {
  return result.findings.reduce((total, finding) => total + finding.pointCost, 0);
}

export type ScoreBand = "good" | "fair" | "poor";

/** Three bands, not a grade. The number is what the reader takes away; the band only decides a
 *  colour and one sentence, and the thresholds are round so nobody has to reverse-engineer them. */
export function scoreBand(score: number): ScoreBand {
  if (score >= 80) return "good";
  return score >= 55 ? "fair" : "poor";
}

export interface FindingDetail {
  /** Message key under `cvScan.findings.<code>.details`. */
  key: string;
  args?: Record<string, number>;
}

/**
 * The specific lines under a finding's heading, chosen from the metrics the check reported.
 *
 * Metrics arrive as named numbers rather than as a pre-built sentence precisely so this can happen
 * in the reader's own language — a server that returned "2 columns detected" would be returning
 * English to a Turkish page.
 */
export function findingDetails(finding: CvScanFinding): FindingDetail[] {
  const metrics = finding.metrics;

  switch (finding.code) {
    case "NoTextLayer":
      return metrics.emptyPageCount
        ? [{ key: "emptyPages", args: { count: metrics.emptyPageCount } }]
        : [{ key: "noText", args: { wordCount: metrics.wordCount ?? 0 } }];

    case "BrokenTurkishCharacters":
      return metrics.unreadableCharacterCount
        ? [{ key: "unmapped", args: { count: metrics.unreadableCharacterCount } }]
        : [{ key: "stripped" }];

    case "MultiColumnOrTableLayout":
      return metrics.tableCount
        ? [{ key: "tables", args: { count: metrics.tableCount } }]
        : [{ key: "columns", args: { position: Math.round(metrics.gutterPositionPercent ?? 50) } }];

    case "SectionsOrDatesUnreadable": {
      const details: FindingDetail[] = [];
      if (metrics.missingExperience) details.push({ key: "missingExperience" });
      if (metrics.missingEducation) details.push({ key: "missingEducation" });
      if (metrics.missingSkills) details.push({ key: "missingSkills" });
      if (!metrics.dateRangeCount) details.push({ key: "noDateRanges" });
      return details;
    }

    case "ContactUnreadable": {
      const details: FindingDetail[] = [];
      if (!metrics.emailFound) details.push({ key: "noEmail" });
      if (!metrics.phoneFound) details.push({ key: "noPhone" });
      if (metrics.onlyInHeaderFooter) details.push({ key: "onlyInHeaderFooter" });
      return details;
    }

    case "LengthOutOfRange":
      if ((metrics.wordCount ?? 0) < 150) {
        return [{ key: "short", args: { wordCount: metrics.wordCount ?? 0 } }];
      }

      return [
        {
          // A .docx has no page count of its own, so the number is an estimate and has to be
          // labelled as one — an invented figure next to an evidenced finding would undo the
          // point of evidencing anything.
          key: metrics.pageCountEstimated ? "longEstimated" : "long",
          args: { pageCount: metrics.pageCount ?? 0 },
        },
      ];

    case "InconsistentFormatting":
      return [
        {
          key: "fonts",
          args: {
            families: metrics.fontFamilyCount ?? 0,
            sizes: metrics.fontSizeCount ?? 0,
          },
        },
      ];

    default:
      return [];
  }
}
