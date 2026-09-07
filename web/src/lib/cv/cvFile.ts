import type { CvFileFormat } from "@/types/api";

/**
 * The two things about a picked file that can be answered without an upload round-trip. The server
 * re-checks both — and additionally checks the file's leading bytes, which the browser has no
 * reason to read — so this only buys a faster, more specific message, never a weaker rule.
 * See CvFileRules on the backend for the boundary that actually holds.
 */
export type CvFileProblem = "unsupportedType" | "tooLarge";

/** Matches StorageOptions.MaxFileSizeBytes. Duplicated rather than fetched: it is a constant of
 *  the product, and a wrong copy here fails safe (the server still refuses). */
export const MAX_CV_FILE_SIZE_BYTES = 10 * 1024 * 1024;

const ACCEPTED_EXTENSIONS = [".pdf", ".doc", ".docx"] as const;

/** The `accept` attribute for the file input. Extensions rather than MIME types: Windows reports
 *  .doc/.docx inconsistently, and the extension is what the server keys off anyway. */
export const CV_FILE_ACCEPT = ACCEPTED_EXTENSIONS.join(",");

export function inspectCvFile(file: { name: string; size: number }): CvFileProblem | null {
  const name = file.name.trim().toLowerCase();

  if (!ACCEPTED_EXTENSIONS.some((extension) => name.endsWith(extension))) {
    return "unsupportedType";
  }

  return file.size > MAX_CV_FILE_SIZE_BYTES ? "tooLarge" : null;
}

/** Short label for the format badge. */
export function cvFormatLabel(format: CvFileFormat): string {
  return format.toUpperCase();
}

/** Only PDFs can be previewed in the browser; there is no way to render a .doc/.docx first page
 *  client-side, and rendering one server-side would mean putting LibreOffice in the API image. */
export function canPreview(format: CvFileFormat): boolean {
  return format === "Pdf";
}
