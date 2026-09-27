import { describe, expect, it } from "vitest";
import { daysAgo } from "./daysAgo";

describe("daysAgo", () => {
  const now = new Date(2026, 8, 27, 9, 0);
  const localIso = (y: number, m: number, d: number, h = 12) => new Date(y, m - 1, d, h).toISOString();

  it("names the recent days", () => {
    expect(daysAgo(localIso(2026, 9, 27), "tr", now)).toBe("bugün");
    expect(daysAgo(localIso(2026, 9, 26), "tr", now)).toBe("dün");
    expect(daysAgo(localIso(2026, 9, 25), "tr", now)).toBe("2 gün önce");
    expect(daysAgo(localIso(2026, 9, 25), "en", now)).toBe("2 days ago");
    expect(daysAgo(localIso(2026, 9, 15), "tr", now)).toBe("12 gün önce");
    expect(daysAgo(localIso(2026, 9, 15), "en", now)).toBe("12 days ago");
  });

  it("counts calendar days, not 24-hour blocks", () => {
    const lateYesterday = new Date(2026, 8, 26, 23, 50).toISOString();
    expect(daysAgo(lateYesterday, "en", new Date(2026, 8, 27, 0, 10))).toBe("yesterday");
  });

  it("switches to months and years for older dates", () => {
    expect(daysAgo(localIso(2026, 6, 20), "tr", now)).toBe("3 ay önce");
    expect(daysAgo(localIso(2024, 9, 1), "en", now)).toBe("2 years ago");
  });

  it("never reads a future date as 'in N days'", () => {
    expect(daysAgo(localIso(2026, 9, 29), "en", now)).toBe("today");
  });
});
