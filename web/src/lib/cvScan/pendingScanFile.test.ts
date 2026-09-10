import { afterEach, describe, expect, it, vi } from "vitest";
import { stashScanFile, takeScanFile } from "./pendingScanFile";

// The store never inspects the file, so a stub with the two fields anything downstream reads is
// enough — and keeps this test out of the business of constructing Blobs.
const file = (name: string) => ({ name, size: 1000 }) as unknown as File;

afterEach(() => {
  vi.useRealTimers();
  // Drain whatever a test left behind: the module is a singleton and the next test starts empty.
  takeScanFile();
});

describe("the landing-to-scan handoff", () => {
  it("hands over the file it was given", () => {
    const dropped = file("cv.pdf");
    stashScanFile(dropped);

    expect(takeScanFile()?.file).toBe(dropped);
  });

  /** Back, then forward, must not resurrect a file the visitor has already dealt with. */
  it("gives the file up only once", () => {
    stashScanFile(file("cv.pdf"));

    expect(takeScanFile()).not.toBeNull();
    expect(takeScanFile()).toBeNull();
  });

  it("returns nothing when nobody dropped anything", () => {
    expect(takeScanFile()).toBeNull();
  });

  /** An interrupted navigation should not park a CV in memory for the rest of the session. */
  it("forgets a file nobody came to collect", () => {
    vi.useFakeTimers();
    stashScanFile(file("cv.pdf"));

    vi.advanceTimersByTime(5 * 60_000 + 1);

    expect(takeScanFile()).toBeNull();
  });

  it("keeps only the most recent file", () => {
    stashScanFile(file("first.pdf"));
    const second = file("second.pdf");
    stashScanFile(second);

    expect(takeScanFile()?.file).toBe(second);
    expect(takeScanFile()).toBeNull();
  });

  /** The scan page seeds its form-timing guard from this, so it has to be the moment of the drop. */
  it("records when the file was picked", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-10T10:00:00Z"));

    stashScanFile(file("cv.pdf"));

    expect(takeScanFile()?.pickedAt).toBe(Date.parse("2026-09-10T10:00:00Z"));
  });
});
