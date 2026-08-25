"use client";

import { useEffect, useState } from "react";
import DOMPurify from "dompurify";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";

interface JobDescriptionCardProps {
  descriptionHtml: string;
}

// Re-sanitizes independently of the extension's own capture-time allow-list (popup.js) — stored
// HTML is untrusted regardless of which side produced it (DECISIONS.md Sprint 9 follow-up); this
// is the actual security boundary, since it runs immediately before dangerouslySetInnerHTML.
const SANITIZE_CONFIG = {
  ALLOWED_TAGS: ["p", "br", "strong", "b", "em", "i", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6"],
  ALLOWED_ATTR: [],
};

export function JobDescriptionCard({ descriptionHtml }: JobDescriptionCardProps) {
  const t = useTranslations("applications.detail.jobDescription");
  const [expanded, setExpanded] = useState(false);
  // DOMPurify.sanitize needs a real `window` (unavailable during Next.js SSR — see
  // DECISIONS.md) — deferred to an effect so it only ever runs client-side, after hydration.
  const [safeHtml, setSafeHtml] = useState<string | null>(null);

  useEffect(() => {
    // Not a derivable value: this is the standard client-only-render bridge (DOMPurify needs
    // `window`, absent during SSR), not state that could instead be computed during render.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSafeHtml(DOMPurify.sanitize(descriptionHtml, SANITIZE_CONFIG));
  }, [descriptionHtml]);

  if (safeHtml === null) {
    return null;
  }

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-5">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <span className="text-xs text-gray-500 dark:text-gray-500">{t("source")}</span>
      </div>

      <div className="relative overflow-hidden" style={{ maxHeight: expanded ? "none" : "180px" }}>
        <div
          className="job-description-prose text-sm leading-relaxed text-gray-700 dark:text-gray-300
            [&_h1,&_h2,&_h3,&_h4,&_h5,&_h6]:mt-5 [&_h1,&_h2,&_h3,&_h4,&_h5,&_h6]:mb-2
            [&_h1,&_h2,&_h3,&_h4,&_h5,&_h6]:text-sm [&_h1,&_h2,&_h3,&_h4,&_h5,&_h6]:font-semibold
            [&_h1,&_h2,&_h3,&_h4,&_h5,&_h6]:text-gray-900 dark:[&_h1,&_h2,&_h3,&_h4,&_h5,&_h6]:text-gray-100
            [&_p]:mb-3 [&_ul]:mb-3 [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:mb-3 [&_ol]:list-decimal [&_ol]:pl-5
            [&_li]:mb-1.5 [&_strong]:font-semibold [&_strong]:text-gray-900 dark:[&_strong]:text-gray-100"
          // Sanitized immediately above with DOMPurify — see SANITIZE_CONFIG.
          dangerouslySetInnerHTML={{ __html: safeHtml }}
        />
        {!expanded && (
          <div className="absolute inset-x-0 bottom-0 h-16 bg-gradient-to-t from-white dark:from-gray-900 to-transparent" />
        )}
      </div>

      <div className="mt-3">
        <Button variant="secondary" onClick={() => setExpanded((prev) => !prev)}>
          {expanded ? t("collapse") : t("expand")}
        </Button>
      </div>
    </div>
  );
}
