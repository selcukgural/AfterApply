import { describe, expect, it } from "vitest";
import { postAuthDestination, sanitizeReturnTo } from "./postAuthRedirect";

// This is the open-redirect guard for the extension pairing flow: the `?next=` parameter on the
// login and sign-up pages, and the `returnTo` carried through the OAuth round trip, both end up
// here before anything navigates. A `next` that accepts arbitrary paths is the classic way to grow
// an open redirect, and this one sits in the middle of a flow that ends in a credential being
// issued — the worst possible place for it.
describe("sanitizeReturnTo", () => {
  it("accepts the pairing page, with and without a code", () => {
    expect(sanitizeReturnTo("/pair")).toBe("/pair");
    expect(sanitizeReturnTo("/pair?code=K7M3QXAB")).toBe("/pair?code=K7M3QXAB");
    expect(sanitizeReturnTo("/pair?code=K7M3-QXAB")).toBe("/pair?code=K7M3-QXAB");
  });

  it("rejects anything that leaves the site", () => {
    for (const hostile of [
      "https://evil.example",
      "//evil.example",
      "//evil.example/pair",
      "http://localhost:3000/pair",
      "/\\evil.example",
      "javascript:alert(1)",
    ]) {
      expect(sanitizeReturnTo(hostile), `${hostile} must not be a destination`).toBeNull();
    }
  });

  it("rejects internal paths it was not asked to allow", () => {
    // An allowlist of one shape, not a sanitiser: /settings is a perfectly safe page and still
    // does not belong here, because widening this is a decision rather than an accident.
    for (const path of ["/dashboard", "/settings", "/pair/extra", "/pairing", "/pair#code=X", "/pair?code=", "/pair?other=1"]) {
      expect(sanitizeReturnTo(path), `${path} must not be a destination`).toBeNull();
    }
  });

  it("accepts the review-writing page, with and without a company slug", () => {
    expect(sanitizeReturnTo("/my-reviews/write")).toBe("/my-reviews/write");
    expect(sanitizeReturnTo("/my-reviews/write?company=turk-telekom-a-s")).toBe("/my-reviews/write?company=turk-telekom-a-s");
  });

  it("rejects a review return path that carries anything but a slug", () => {
    for (const path of ["/my-reviews", "/my-reviews/write?company=", "/my-reviews/write?company=Türk", "/my-reviews/write?company=a&x=1", "/my-reviews/write/extra"]) {
      expect(sanitizeReturnTo(path), `${path} must not be a destination`).toBeNull();
    }
  });

  it("rejects a code longer than any code the server issues", () => {
    expect(sanitizeReturnTo(`/pair?code=${"A".repeat(17)}`)).toBeNull();
  });

  it("treats absent and empty input as no destination", () => {
    expect(sanitizeReturnTo(null)).toBeNull();
    expect(sanitizeReturnTo(undefined)).toBeNull();
    expect(sanitizeReturnTo("")).toBeNull();
  });
});

describe("postAuthDestination", () => {
  it("falls back to the dashboard, which is where a normal sign-in goes", () => {
    expect(postAuthDestination(null)).toBe("/dashboard");
    expect(postAuthDestination("https://evil.example")).toBe("/dashboard");
  });

  it("hands back a pending pairing so the extension flow survives a sign-up", () => {
    expect(postAuthDestination("/pair?code=K7M3QXAB")).toBe("/pair?code=K7M3QXAB");
  });
});
