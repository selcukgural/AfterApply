import { describe, expect, it } from "vitest";
import { switchedOff, withoutSwitchedOff } from "./switchedOff";
import { DEFAULT_CLIENT_CONFIG } from "@/lib/api/config";

describe("switchedOff", () => {
  it("withdraws nothing while the answer is not in", () => {
    expect(switchedOff(null)).toEqual({ guide: false, cvScan: false, companies: false, suggestions: false });
    expect(switchedOff(undefined)).toEqual({ guide: false, cvScan: false, companies: false, suggestions: false });
  });

  it("withdraws nothing for fields an older API does not send", () => {
    expect(switchedOff({})).toEqual({ guide: false, cvScan: false, companies: false, suggestions: false });
  });

  it("withdraws exactly what the server says is off", () => {
    expect(
      switchedOff({
        blog: { enabled: false, hasPublishedPosts: false },
        cvScan: { enabled: false, contentNotesAvailable: false },
        companyReviews: { enabled: false, maxReviewsPerUser: 10, minimumReviewsForScore: 3, priorWeight: 5 },
        emailSignals: { enabled: false },
      }),
    ).toEqual({ guide: true, cvScan: true, companies: true, suggestions: true });
    expect(switchedOff({ blog: { enabled: true, hasPublishedPosts: false }, emailSignals: { enabled: true } })).toEqual({
      guide: false,
      cvScan: false,
      companies: false,
      suggestions: false,
    });
  });

  it("is never fed the built-in default as if the server had answered", () => {
    // The default says "off" for several features on purpose (they show only once the server says
    // on). Read as an answer it would hide the guide and the scan on every first paint, so callers
    // pass null until the query has resolved.
    expect(switchedOff(DEFAULT_CLIENT_CONFIG).guide).toBe(true);
  });
});

describe("withoutSwitchedOff", () => {
  const links = [{ key: "howItWorks" }, { key: "companies" }, { key: "tools" }, { key: "guide" }, { key: "cvScan" }, { key: "help" }];
  const nothingOff = switchedOff(null);

  it("keeps every link while nothing is switched off", () => {
    expect(withoutSwitchedOff(links, nothingOff)).toEqual(links);
  });

  it("drops exactly the links of the features that are off", () => {
    expect(withoutSwitchedOff(links, { ...nothingOff, guide: true }).map((link) => link.key)).not.toContain("guide");
    expect(withoutSwitchedOff(links, { ...nothingOff, cvScan: true }).map((link) => link.key)).toEqual([
      "howItWorks",
      "companies",
      "tools",
      "guide",
      "help",
    ]);
    expect(withoutSwitchedOff(links, { guide: true, cvScan: true, companies: true, suggestions: true }).map((link) => link.key)).toEqual([
      "howItWorks",
      "tools",
      "help",
    ]);
  });
});
