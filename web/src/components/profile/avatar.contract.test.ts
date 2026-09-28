import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { COMMENT_REPORT_REASONS, commentReportReasons } from "@/components/blog/comments/ReportCommentDialog";

/**
 * Source scan, like the other contract tests. The profile photo (DECISIONS.md 2026-09-28) reaches
 * the owner's menu and profile, blog comments and the comment moderation — and never the anonymous
 * company surfaces. The API holds that line (AvatarExposureTests); this is the page's half of it,
 * so a component there cannot start drawing a photo from some other response either.
 */
const SRC = path.join(process.cwd(), "src");
const messages = (locale: string) => JSON.parse(readFileSync(path.join(process.cwd(), "messages", `${locale}.json`), "utf8"));

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const full = path.join(directory, entry);
    if (statSync(full).isDirectory()) return sourceFiles(full);
    return /\.(ts|tsx)$/.test(entry) && !/\.test\.tsx?$/.test(entry) ? [full] : [];
  });
}

describe("the profile photo", () => {
  it.each([
    "components/companySalaries",
    "components/companyReviews",
    "components/candidateExperiences",
    "components/companyIntelligence",
    "components/responseRates",
    "components/companies",
  ])("never appears in %s", (folder) => {
    const offenders = sourceFiles(path.join(SRC, folder)).filter((file) => /avatarUrl|components\/ui\/Avatar/i.test(readFileSync(file, "utf8")));

    expect(offenders).toEqual([]);
  });

  it("is reportable on a comment only where the comment shows one", () => {
    expect(commentReportReasons(true)).toContain("ProfilePhoto");
    expect(commentReportReasons(false)).not.toContain("ProfilePhoto");
    expect(commentReportReasons(false)).toHaveLength(COMMENT_REPORT_REASONS.length - 1);
  });

  it("names, in both languages, where it never appears", () => {
    for (const locale of ["tr", "en"]) {
      const copy = messages(locale);
      expect(copy.blogComments.reasons.ProfilePhoto).toBeTruthy();
      expect(copy.privacy.profilePhoto.never).toBeTruthy();
    }
    expect(messages("tr").profile.avatar.showInCommentsHint).toContain("asla görünmez");
    expect(messages("en").profile.avatar.showInCommentsHint).toContain("never appears there");
  });
});
