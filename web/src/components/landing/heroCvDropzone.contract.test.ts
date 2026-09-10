import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * The hero's drop zone moves a file and nothing else.
 *
 * Nothing in the type system stops someone adding "just a quick scan" to the hero later, and it
 * would look like an improvement in review. It is not: the consent box, the correction to the
 * "ATS auto-rejects 75% of CVs" myth, the itemised list of what a scan retains and the result
 * screen itself all live on /cv-tarama, and the V6 stopping condition is read off that page's
 * views against completed scans. A scan started here would separate the consent from the reading,
 * and split the funnel's denominator across two pages.
 *
 * A source-scan rather than a render test, matching browserStorage.test.ts: this suite runs in
 * node with no DOM.
 */
describe("the hero drop zone", () => {
  const source = readFileSync(
    path.join(process.cwd(), "src/components/landing/HeroCvDropzone.tsx"),
    "utf8",
  );

  it("never scans, and never asks for consent", () => {
    // Asserted on the identifiers rather than on the word: the file's own doc comment explains
    // where consent is collected, and a test that forbade the word would forbid the explanation.
    expect(source).not.toContain("cvScanApi");
    expect(source).not.toContain("consentAccepted");
    expect(source).not.toContain("useMutation");
  });

  it("hands the file over instead of keeping it", () => {
    expect(source).toContain("stashScanFile");
    expect(source).toContain("/cv-tarama");
  });

  /** Persisting a stranger's CV would put it on their disk and make the published cookie policy
   *  false; the handoff is memory-only by construction. */
  it("writes nothing to the browser's storage", () => {
    expect(source).not.toContain("localStorage");
    expect(source).not.toContain("sessionStorage");
    expect(source).not.toContain("indexedDB");
  });
});
