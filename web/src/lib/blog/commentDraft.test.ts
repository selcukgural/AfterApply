import { describe, expect, it } from "vitest";
import { COMMENT_MAX_LENGTH, relativeTime, validateCommentDraft } from "./commentDraft";

describe("validateCommentDraft", () => {
  it("wants ten to three thousand characters once trimmed", () => {
    expect(validateCommentDraft("")).toBe("empty");
    expect(validateCommentDraft("   \n ")).toBe("empty");
    expect(validateCommentDraft("kısa")).toBe("tooShort");
    expect(validateCommentDraft("  dokuzhrf  ")).toBe("tooShort");
    expect(validateCommentDraft("Tam on kar.")).toBeNull();
    expect(validateCommentDraft("a".repeat(COMMENT_MAX_LENGTH))).toBeNull();
    expect(validateCommentDraft("a".repeat(COMMENT_MAX_LENGTH + 1))).toBe("tooLong");
  });
});

describe("relativeTime", () => {
  const now = new Date("2026-09-20T12:00:00Z");
  const ago = (ms: number) => new Date(now.getTime() - ms).toISOString();

  it("speaks the locale's own relative phrases up to a week", () => {
    expect(relativeTime(ago(10_000), "tr", now)).toBe("şimdi");
    expect(relativeTime(ago(5 * 60_000), "tr", now)).toBe("5 dakika önce");
    expect(relativeTime(ago(2 * 3_600_000), "tr", now)).toBe("2 saat önce");
    expect(relativeTime(ago(24 * 3_600_000), "tr", now)).toBe("dün");
    expect(relativeTime(ago(3 * 24 * 3_600_000), "en", now)).toBe("3 days ago");
  });

  it("falls back to the date past a week", () => {
    expect(relativeTime(ago(30 * 24 * 3_600_000), "tr", now)).toBe("21 Ağu 2026");
    expect(relativeTime(ago(30 * 24 * 3_600_000), "en", now)).toBe("Aug 21, 2026");
  });
});
