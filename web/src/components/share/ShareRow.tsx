"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useTranslations } from "next-intl";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";
import { SHARE_TARGETS, type ShareContent, type ShareTarget, shareClipboardText, shareHref } from "@/lib/share/shareLinks";

interface ShareRowProps {
  /** The button's label — "Share your score", "Share your result", "Share". */
  label: string;
  content: ShareContent;
}

const TARGET_LABEL: Record<ShareTarget, string> = { linkedin: "LinkedIn", whatsapp: "WhatsApp", x: "X" };

// navigator.share never changes during a page's life, so there is nothing to subscribe to.
const subscribeToNothing = () => () => {};

/**
 * The one share control on the public site (growth audit 2026-09-14, findings 03 and 14). Where
 * the browser has a native share sheet — phones, mostly — a single button opens it; elsewhere a
 * row of plain links to LinkedIn, WhatsApp and X plus "copy link". Every use reports one
 * `share_clicked` to the visit counter: whether the product produces anything a person wants to
 * pass on is the number the audit could not read, and this is where it comes from.
 *
 * No third-party script, no widget, nothing stored: the links are navigations to the networks'
 * own share pages, and the clipboard write is the browser's.
 */
export function ShareRow({ label, content }: ShareRowProps) {
  const t = useTranslations("share");
  // Whether the browser has a native share sheet. Read through useSyncExternalStore so the
  // server (which cannot know) renders the link row, hydration matches it, and React switches
  // to the single button on the client without a mismatch or a setState-in-effect.
  const canShare = useSyncExternalStore(
    subscribeToNothing,
    () => typeof navigator.share === "function",
    () => false,
  );
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(timer);
  }, [copied]);

  const report = () => trackSiteTraffic("share_clicked");

  const nativeShare = async () => {
    report();
    try {
      await navigator.share({ text: content.text, url: content.url });
    } catch {
      // The person closed the sheet, or the browser refused — neither is an error to show.
    }
  };

  const copy = async () => {
    report();
    try {
      await navigator.clipboard.writeText(shareClipboardText(content));
      setCopied(true);
    } catch {
      // No clipboard permission: the links beside the button still work.
    }
  };

  const linkClass =
    "rounded-md border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800";

  if (canShare) {
    return (
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" onClick={nativeShare} className={linkClass}>
          {label}
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-wrap items-center gap-2" role="group" aria-label={label}>
      <span className="text-sm text-gray-600 dark:text-gray-400">{label}:</span>
      {SHARE_TARGETS.map((target) => (
        <a
          key={target}
          href={shareHref(target, content)}
          target="_blank"
          rel="noopener noreferrer"
          onClick={report}
          className={linkClass}
        >
          {TARGET_LABEL[target]}
        </a>
      ))}
      <button type="button" onClick={copy} className={linkClass} aria-live="polite">
        {copied ? t("copied") : t("copyLink")}
      </button>
    </div>
  );
}
