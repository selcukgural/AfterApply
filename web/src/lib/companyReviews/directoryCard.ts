import type { CompanyPublicListItem } from "@/types/api";

export type DirectoryCountKey = "reviewCount" | "salaryCount" | "experienceCount";

export interface DirectoryCountLine {
  key: DirectoryCountKey;
  count: number;
}

/**
 * The count lines under a directory card's name (2026-09-18): one per kind the company actually
 * has, in the order of the company page's tabs. A zero is not a line — "0 salary entries" on
 * every card would drown the one number that matters — except that a card always has at least
 * one line, so a company with nothing (which the server no longer lists, but the rule is here)
 * still says "0 reviews" rather than nothing at all.
 */
export function directoryCountLines(item: Pick<CompanyPublicListItem, "approvedCount" | "salaryCount" | "candidateExperienceCount">): DirectoryCountLine[] {
  const all: DirectoryCountLine[] = [
    { key: "reviewCount", count: item.approvedCount },
    { key: "salaryCount", count: item.salaryCount },
    { key: "experienceCount", count: item.candidateExperienceCount },
  ];
  const lines = all.filter((line) => line.count > 0);
  return lines.length > 0 ? lines : [{ key: "reviewCount", count: 0 }];
}
