import { describe, expect, it } from "vitest";
import { formatFileSize, inspectLinkedInExportFile } from "./linkedInExportFile";

describe("inspectLinkedInExportFile", () => {
  it("accepts the archive LinkedIn hands out", () => {
    expect(inspectLinkedInExportFile({ name: "Basic_LinkedInDataExport_09-06-2026.zip", size: 84_000 })).toBeNull();
  });

  it("accepts a .zip regardless of case", () => {
    expect(inspectLinkedInExportFile({ name: "EXPORT.ZIP", size: 10 })).toBeNull();
  });

  it("flags an unzipped CSV separately from any other wrong file", () => {
    expect(inspectLinkedInExportFile({ name: "Job Applications.csv", size: 4_200 })).toBe("unzipped");
    expect(inspectLinkedInExportFile({ name: "resume.pdf", size: 4_200 })).toBe("notZip");
  });

  it("flags an empty archive rather than sending a zero-byte upload", () => {
    expect(inspectLinkedInExportFile({ name: "export.zip", size: 0 })).toBe("empty");
  });

  it("ignores surrounding whitespace in the file name", () => {
    expect(inspectLinkedInExportFile({ name: " export.zip ", size: 10 })).toBeNull();
  });
});

describe("formatFileSize", () => {
  it("keeps small files in bytes", () => {
    expect(formatFileSize(512, "en")).toBe("512 B");
  });

  it("rounds kilobytes to whole units", () => {
    expect(formatFileSize(84_000, "en")).toBe("82 KB");
  });

  it("uses the locale's decimal separator for megabytes", () => {
    expect(formatFileSize(3_500_000, "en")).toBe("3.3 MB");
    expect(formatFileSize(3_500_000, "tr")).toBe("3,3 MB");
  });
});
