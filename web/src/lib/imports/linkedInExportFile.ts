/**
 * The two ways handing us a LinkedIn export actually goes wrong, caught in the browser so the
 * answer arrives before an upload round-trip does. The backend re-checks both
 * (ImportService.StageLinkedInZipImportAsync) and stays the real boundary — this only buys a
 * faster and more specific message than "Only .zip files are supported."
 */
export type LinkedInExportFileProblem = "unzipped" | "notZip" | "empty";

export function inspectLinkedInExportFile(file: { name: string; size: number }): LinkedInExportFileProblem | null {
  const name = file.name.trim().toLowerCase();

  if (!name.endsWith(".zip")) {
    // A .csv means they opened the archive and picked "Job Applications.csv" out of it — the
    // single most common mistake, and it deserves its own answer rather than a generic one.
    return name.endsWith(".csv") ? "unzipped" : "notZip";
  }

  return file.size <= 0 ? "empty" : null;
}

/**
 * Byte counts shown next to the picked file. Locale-aware because the decimal separator differs
 * between the two languages the app ships in, and "1.4 MB" vs "1,4 MB" is exactly the kind of
 * detail that makes a page read as translated rather than localized.
 */
export function formatFileSize(bytes: number, locale: string): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const kilobytes = bytes / 1024;
  if (kilobytes < 1024) {
    return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(kilobytes)} KB`;
  }

  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(kilobytes / 1024)} MB`;
}
