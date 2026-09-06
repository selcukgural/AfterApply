import type { LinkedInArchiveDiagramLabels } from "./LinkedInArchiveDiagram";

/**
 * The sketch is rendered from both the client-side import page (`useTranslations`) and the
 * server-side help page (`getTranslations`); both hand back a callable of this shape, so the
 * labels are built once here instead of being spelled out at each call site.
 */
export function archiveDiagramLabels(t: (key: string) => string): LinkedInArchiveDiagramLabels {
  return {
    a11y: t("a11y"),
    caption: t("caption"),
    heading: t("heading"),
    fullOption: t("fullOption"),
    fullEta: t("fullEta"),
    pickOption: t("pickOption"),
    pickEta: t("pickEta"),
    jobs: t("jobs"),
    otherOption1: t("otherOption1"),
    otherOption2: t("otherOption2"),
    cta: t("cta"),
    pointer: t("pointer"),
  };
}
