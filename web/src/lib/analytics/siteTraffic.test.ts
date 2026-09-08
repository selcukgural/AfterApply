import { describe, expect, it } from "vitest";
import { buildSiteTrafficPayload, isDoNotTrackEnabled } from "./siteTraffic";

// What is under test is the browser's half of the privacy promise: the API strips these things
// again, but by then they have already crossed the network. These rules are what stop them
// leaving the visitor's machine in the first place.

describe("buildSiteTrafficPayload", () => {
  it("keeps the path and drops the query string", () => {
    expect(buildSiteTrafficPayload("page_view", "/tr/guide/how-many-applications", null).path).toBe(
      "/tr/guide/how-many-applications",
    );
    expect(buildSiteTrafficPayload("page_view", "/tr/login?next=%2Ftr%2Fdashboard", null).path).toBe("/tr/login");
  });

  // The case this rule exists for: the reset link's token is in the query, and it is a credential.
  it("never lets a reset token leave the browser", () => {
    const payload = buildSiteTrafficPayload("page_view", "/tr/reset-password?token=abc123secret", null);

    expect(payload.path).toBe("/tr/reset-password");
    expect(JSON.stringify(payload)).not.toContain("secret");
  });

  it("drops a fragment as well, in either order", () => {
    expect(buildSiteTrafficPayload("page_view", "/tr/guide#top", null).path).toBe("/tr/guide");
    expect(buildSiteTrafficPayload("page_view", "/tr/guide?a=1#top", null).path).toBe("/tr/guide");
    expect(buildSiteTrafficPayload("page_view", "/tr/guide#top?a=1", null).path).toBe("/tr/guide");
  });

  it("reduces a referrer to its origin, so the search term stays behind", () => {
    const payload = buildSiteTrafficPayload(
      "page_view",
      "/tr",
      "https://www.google.com/search?q=is+basvuru+takip+programi",
    );

    expect(payload.referrer).toBe("https://www.google.com");
    expect(payload.referrer).not.toContain("q=");
  });

  it("returns null for a referrer that is missing, relative or not a web page", () => {
    for (const referrer of [null, undefined, "", "/tr/dashboard", "javascript:alert(1)", "data:text/html,hi", "nonsense"]) {
      expect(buildSiteTrafficPayload("page_view", "/tr", referrer).referrer).toBeNull();
    }
  });

  it("carries nothing but the three declared fields", () => {
    // A visitor id added here would be invisible in review but would break the claim on /cookies.
    expect(Object.keys(buildSiteTrafficPayload("page_view", "/tr", null)).sort()).toEqual([
      "event",
      "path",
      "referrer",
    ]);
  });
});

describe("isDoNotTrackEnabled", () => {
  it("honours both values browsers have used", () => {
    expect(isDoNotTrackEnabled("1")).toBe(true);
    expect(isDoNotTrackEnabled("yes")).toBe(true);
  });

  it("counts when the visitor has expressed no preference", () => {
    for (const value of ["0", "unspecified", null, undefined, ""]) {
      expect(isDoNotTrackEnabled(value)).toBe(false);
    }
  });
});
