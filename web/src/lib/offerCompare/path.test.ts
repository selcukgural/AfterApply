import { describe, expect, it } from "vitest";
import { offerComparePath, offerCompareRedirectForPath } from "./path";

describe("offerComparePath", () => {
  it("is /teklif-karsilastirma under tr and /offer-comparison under en, defaulting to Turkish", () => {
    expect(offerComparePath("tr")).toBe("/teklif-karsilastirma");
    expect(offerComparePath("en")).toBe("/offer-comparison");
    expect(offerComparePath("de")).toBe("/teklif-karsilastirma");
  });
});

describe("offerCompareRedirectForPath", () => {
  it("sends the wrong slug to the right one, in both directions", () => {
    expect(offerCompareRedirectForPath("/en/teklif-karsilastirma")).toBe("/en/offer-comparison");
    expect(offerCompareRedirectForPath("/tr/offer-comparison/")).toBe("/tr/teklif-karsilastirma");
  });

  it("leaves the right addresses and everything else alone", () => {
    expect(offerCompareRedirectForPath("/tr/teklif-karsilastirma")).toBeNull();
    expect(offerCompareRedirectForPath("/en/offer-comparison")).toBeNull();
    expect(offerCompareRedirectForPath("/offer-comparison")).toBeNull();
    expect(offerCompareRedirectForPath("/tr/help/offer-comparison")).toBeNull();
  });
});
