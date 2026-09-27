import { describe, expect, it } from "vitest";
import { countdown, excerpt, isInterviewMorning, spaceBlocks } from "./interviewMorning";

const at = (h: number, m = 0, d = 27) => new Date(2026, 8, d, h, m).toISOString();

describe("isInterviewMorning", () => {
  const now = new Date(2026, 8, 27, 10, 40);

  it("is true for an interview later today", () => {
    expect(isInterviewMorning(at(14), now)).toBe(true);
  });

  it("stays for two hours after the start, then hands over", () => {
    expect(isInterviewMorning(at(9), now)).toBe(true);
    expect(isInterviewMorning(at(8, 30), now)).toBe(false);
  });

  it("is false for tomorrow or yesterday", () => {
    expect(isInterviewMorning(at(9, 0, 28), now)).toBe(false);
    expect(isInterviewMorning(at(18, 0, 26), now)).toBe(false);
  });
});

describe("countdown", () => {
  const now = new Date(2026, 8, 27, 10, 40);

  it("gives hours and minutes left, rounded up", () => {
    expect(countdown(at(14), now)).toEqual({ kind: "later", hours: 3, minutes: 20 });
    expect(countdown(new Date(now.getTime() + 30_000).toISOString(), now)).toEqual({ kind: "later", hours: 0, minutes: 1 });
  });

  it("says started once the time has come", () => {
    expect(countdown(at(10, 40), now)).toEqual({ kind: "started" });
    expect(countdown(at(9), now)).toEqual({ kind: "started" });
  });
});

describe("excerpt", () => {
  it("keeps a short text whole and collapses whitespace", () => {
    expect(excerpt("  We are\n hiring  ")).toBe("We are hiring");
  });

  it("cuts a long text at a word and marks the cut", () => {
    const text = "word ".repeat(100);
    const result = excerpt(text, 23);
    expect(result).toBe("word word word word…");
    expect(result.length).toBeLessThanOrEqual(24);
  });
});

describe("spaceBlocks", () => {
  it("separates paragraphs, list items and line breaks", () => {
    expect(spaceBlocks("<p>One.</p><ul><li>Queues</li><li>Observability</li></ul>Two<br>Three")).toBe(
      "<p>One.</p> <ul><li>Queues</li> <li>Observability</li> </ul> Two<br> Three",
    );
  });
});
