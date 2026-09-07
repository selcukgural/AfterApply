import { describe, expect, it } from "vitest";
import { CV_FILE_ACCEPT, MAX_CV_FILE_SIZE_BYTES, canPreview, cvFormatLabel, inspectCvFile } from "./cvFile";

describe("inspectCvFile", () => {
  it("accepts the three supported extensions, whatever their case", () => {
    for (const name of ["cv.pdf", "CV.PDF", "ozgecmis.doc", "ozgecmis.DOCX"]) {
      expect(inspectCvFile({ name, size: 1024 })).toBeNull();
    }
  });

  it("refuses anything else", () => {
    for (const name of ["cv.exe", "cv.png", "cv", "cv.pdf.exe"]) {
      expect(inspectCvFile({ name, size: 1024 })).toBe("unsupportedType");
    }
  });

  it("refuses a file over the size cap", () => {
    expect(inspectCvFile({ name: "cv.pdf", size: MAX_CV_FILE_SIZE_BYTES + 1 })).toBe("tooLarge");
    expect(inspectCvFile({ name: "cv.pdf", size: MAX_CV_FILE_SIZE_BYTES })).toBeNull();
  });

  it("names the extension as the problem before the size", () => {
    // Otherwise a huge .exe would be reported as "too large", which tells the user to shrink a
    // file that was never going to be accepted.
    expect(inspectCvFile({ name: "cv.exe", size: MAX_CV_FILE_SIZE_BYTES + 1 })).toBe("unsupportedType");
  });

  it("ignores surrounding whitespace in the name", () => {
    expect(inspectCvFile({ name: "  cv.pdf  ", size: 1024 })).toBeNull();
  });
});

describe("CV_FILE_ACCEPT", () => {
  it("lists exactly the extensions the server accepts", () => {
    expect(CV_FILE_ACCEPT).toBe(".pdf,.doc,.docx");
  });
});

describe("canPreview", () => {
  it("is true only for PDFs", () => {
    expect(canPreview("Pdf")).toBe(true);
    expect(canPreview("Doc")).toBe(false);
    expect(canPreview("Docx")).toBe(false);
  });
});

describe("cvFormatLabel", () => {
  it("renders the badge text", () => {
    expect(cvFormatLabel("Pdf")).toBe("PDF");
    expect(cvFormatLabel("Docx")).toBe("DOCX");
  });
});
