"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Modal } from "@/components/ui/Modal";
import { buttonClassName } from "@/components/ui/Button";
import { analyticsApi } from "@/lib/api/analytics";
import { MIN_FLOW_CARD_TOTAL, flowCardFromResponse, formatFlowCard, isShareable } from "@/lib/flowCard/card";
import { FLOW_NODE_KEYS, flowCaption } from "@/lib/flowCard/copy";
import { flowCardPath } from "@/lib/flowCard/path";
import { PLATFORM_NAME, SHARE_OUTPUTS, outputSize, type SharePlatform } from "@/lib/flowCard/sharePlan";
import { shareHref } from "@/lib/share/shareLinks";
import { FlowCardPreview } from "@/components/dashboard/FlowCardPreview";
import { SharePlatformPicker } from "@/components/dashboard/SharePlatformPicker";
import { SocialIcon } from "@/components/layout/SocialIcon";
import type { FlowPeriod } from "@/types/api";

const PERIODS: readonly FlowPeriod[] = ["30", "90", "all"];

// Neither the origin nor the browser's file-sharing support changes during a page's life, so there
// is nothing to subscribe to; the server snapshot keeps the first client render equal to the server's.
const subscribeToNothing = () => () => {};

function browserCanShareFiles(): boolean {
  try {
    const probe = new File([new Blob()], "probe.png", { type: "image/png" });
    return typeof navigator.canShare === "function" && navigator.canShare({ files: [probe] });
  } catch {
    return false;
  }
}

const segmentClass = (active: boolean) =>
  `h-9 rounded px-3 text-sm transition-colors ${
    active
      ? "bg-white font-semibold text-gray-900 shadow-sm dark:bg-gray-700 dark:text-gray-100"
      : "text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
  }`;

/**
 * "Share your flow" (2026-09-23, canvas "A · Platformlar"): the person picks where they are
 * posting, then what — an image sized for that network or a link whose preview the network draws —
 * and the period. The preview is the real image from `/[locale]/og`, so what they see is what they
 * post — with the same card drawn in the browser while that image renders (FlowCardPreview).
 *
 * Nothing is uploaded or stored: the card is the counts in its own URL, an image download is a
 * fetch of that URL, and a link share is a navigation to the network's own share page. No visit
 * counter either — this is a signed-in page (lib/privacy/browserStorage.test.ts); whether shared
 * cards bring anyone back shows up on the public card page instead.
 */
export function ShareFlowDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations("flowCard.share");
  const tCard = useTranslations("flowCard.card");
  const tLegend = useTranslations("flowCard.legend");
  const locale = useLocale();

  const [platform, setPlatform] = useState<SharePlatform>("linkedin");
  const [outputIndex, setOutputIndex] = useState(0);
  const [period, setPeriod] = useState<FlowPeriod>("90");

  const { data, isLoading, isError } = useQuery({
    queryKey: ["analytics", "flow", period],
    queryFn: () => analyticsApi.getFlow(period),
  });

  const outputs = SHARE_OUTPUTS[platform];
  const output = outputs[Math.min(outputIndex, outputs.length - 1)];

  const card = data && isShareable(data.counts) ? flowCardFromResponse(data) : null;
  const segment = card ? formatFlowCard(card) : null;
  // Twice the pixels: the preview sits on high-density screens, and a downloaded post is viewed on
  // phones; the networks scale it down themselves (the link preview stays at its standard size).
  const imagePath = segment ? `/${locale}/og?flow=${segment}&f=${output.format}&d=2` : null;

  // Absolute, because it leaves the site (a network's share page, the clipboard, a post's text).
  const origin = useSyncExternalStore(subscribeToNothing, () => window.location.origin, () => "");
  const pageUrl = segment && origin ? `${origin}/${locale}${flowCardPath(locale, segment)}` : "";

  const suggestedCaption = card ? flowCaption(card, period, locale, t) : "";
  const suggestedText = suggestedCaption && pageUrl ? `${suggestedCaption} ${pageUrl}` : suggestedCaption;
  // The person's edit, remembered against the sentence it was made to: a new period is a new
  // sentence, and an edit to the old one does not carry over.
  const [edit, setEdit] = useState<{ base: string; text: string } | null>(null);
  const caption = edit && edit.base === suggestedText ? edit.text : suggestedText;

  const [copied, setCopied] = useState<"caption" | "link" | null>(null);
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(null), 2000);
    return () => clearTimeout(timer);
  }, [copied]);

  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState(false);

  const copy = async (text: string, what: "caption" | "link") => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(what);
    } catch {
      // No clipboard permission: the text is still in the box to select by hand.
    }
  };

  const fetchImage = async (): Promise<File> => {
    const response = await fetch(imagePath!);
    if (!response.ok) throw new Error(`Card image failed: ${response.status}`);
    const blob = await response.blob();
    return new File([blob], `${t("fileName")}-${output.format}.png`, { type: "image/png" });
  };

  const download = async () => {
    if (!imagePath) return;
    setBusy(true);
    setActionError(false);
    try {
      const file = await fetchImage();
      const url = URL.createObjectURL(file);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = file.name;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      setActionError(true);
    } finally {
      setBusy(false);
    }
  };

  // Phones: hand the PNG straight to the app's share sheet (Instagram, WhatsApp…).
  const canShareFiles = useSyncExternalStore(subscribeToNothing, browserCanShareFiles, () => false);

  const shareFile = async () => {
    if (!imagePath) return;
    setBusy(true);
    setActionError(false);
    try {
      const file = await fetchImage();
      await navigator.share({ files: [file], text: caption });
    } catch (error) {
      // Closing the sheet is an AbortError, not a failure to report.
      if (!(error instanceof DOMException && error.name === "AbortError")) setActionError(true);
    } finally {
      setBusy(false);
    }
  };

  const linkTarget = platform === "instagram" ? null : platform;
  // The file is drawn at twice the network's size (imagePath), and the button says so.
  const fileSize = outputSize(output, 2);

  return (
    <Modal
      title={t("title")}
      onClose={onClose}
      busy={busy}
      wide
      footer={
        <button type="button" onClick={onClose} className={buttonClassName("secondary")}>
          {t("close")}
        </button>
      }
    >
      <div className="flex flex-col gap-5">
        <h2 className="text-lg font-semibold">{t("title")}</h2>

        <fieldset className="flex flex-col gap-2">
          <legend className="mb-2 text-sm font-semibold text-gray-600 dark:text-gray-400">{t("platformLabel")}</legend>
          <SharePlatformPicker
            value={platform}
            onChange={(id) => {
              setPlatform(id);
              setOutputIndex(0);
            }}
          />
        </fieldset>

        <div className="flex flex-wrap gap-5">
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-sm font-semibold text-gray-600 dark:text-gray-400">{t("outputLabel")}</legend>
            <div className="flex gap-0.5 rounded-md bg-gray-100 p-0.5 dark:bg-gray-800">
              {outputs.map((option, index) => (
                <button
                  key={option.label}
                  type="button"
                  aria-pressed={option === output}
                  onClick={() => setOutputIndex(index)}
                  className={segmentClass(option === output)}
                >
                  {t(`outputs.${option.label}`)}{" "}
                  <span className="text-xs font-normal text-gray-500 dark:text-gray-400">{outputSize(option)}</span>
                </button>
              ))}
            </div>
          </fieldset>

          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-sm font-semibold text-gray-600 dark:text-gray-400">{t("periodLabel")}</legend>
            <div className="flex gap-0.5 rounded-md bg-gray-100 p-0.5 dark:bg-gray-800">
              {PERIODS.map((value) => (
                <button
                  key={value}
                  type="button"
                  aria-pressed={period === value}
                  onClick={() => setPeriod(value)}
                  className={segmentClass(period === value)}
                >
                  {t(`periods.${value}`)}
                </button>
              ))}
            </div>
          </fieldset>
        </div>

        <div className="flex h-80 items-center justify-center overflow-hidden rounded-md bg-gray-100 p-3 dark:bg-gray-800">
          {isLoading ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>
          ) : isError ? (
            <p className="text-sm text-crit-ink">{t("error")}</p>
          ) : !card ? (
            <p className="max-w-sm text-center text-sm text-gray-600 dark:text-gray-400">
              {t("tooFew", { count: data?.counts.total ?? 0, min: MIN_FLOW_CARD_TOTAL })}
            </p>
          ) : (
            <FlowCardPreview card={card} format={output.format} imagePath={imagePath!} alt={t("previewAlt")} />
          )}
        </div>

        {card ? (
          <>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              {output.kind === "link" ? t("notes.link", { platform: PLATFORM_NAME[platform] }) : t(`notes.${output.note}`)}
            </p>

            {/* What each part of the card means — the card itself has room for names, not definitions. */}
            <details className="rounded-md border border-gray-200 px-3 py-2 text-sm dark:border-gray-800">
              <summary className="cursor-pointer font-medium text-gray-800 dark:text-gray-200">{tLegend("title")}</summary>
              <p className="mt-2 text-gray-600 dark:text-gray-400">{tLegend("intro")}</p>
              <dl className="mt-2 grid gap-x-5 gap-y-1.5 sm:grid-cols-2">
                {FLOW_NODE_KEYS.filter((key) => key !== "total" && card.counts[key] > 0).map((key) => (
                  <div key={key}>
                    <dt className="font-medium text-gray-900 dark:text-gray-100">
                      {tCard(`nodes.${key}`)} · {card.counts[key]}
                    </dt>
                    <dd className="text-gray-600 dark:text-gray-400">{tLegend(`items.${key}`)}</dd>
                  </div>
                ))}
              </dl>
            </details>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="flow-caption" className="text-sm font-semibold text-gray-600 dark:text-gray-400">
                {t("captionLabel")}
              </label>
              <textarea
                id="flow-caption"
                rows={3}
                value={caption}
                onChange={(event) => setEdit({ base: suggestedText, text: event.target.value })}
                className="w-full resize-none rounded-md border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-950"
              />
            </div>

            <div className="flex flex-wrap items-center gap-2">
              {output.kind === "image" ? (
                <>
                  <button type="button" onClick={download} disabled={busy} className={buttonClassName("primary")}>
                    {t("download", { size: fileSize })}
                  </button>
                  {canShareFiles ? (
                    <button type="button" onClick={shareFile} disabled={busy} className={buttonClassName("secondary")}>
                      {t("shareFile")}
                    </button>
                  ) : null}
                </>
              ) : (
                <>
                  {linkTarget && pageUrl ? (
                    <a
                      href={shareHref(linkTarget, { text: suggestedCaption, url: pageUrl })}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={`${buttonClassName("primary")} inline-flex items-center gap-2`}
                    >
                      {/* The site's own button with the network's mark on it: the brand colour stays
                          on the picked circle above, so this is the one filled button on the page. */}
                      <SocialIcon network={linkTarget} className="h-4 w-4" />
                      {t(`shareOn.${linkTarget}`)}
                    </a>
                  ) : null}
                  <button
                    type="button"
                    onClick={async () => {
                                      await copy(pageUrl, "link");
                    }}
                    disabled={!pageUrl}
                    className={buttonClassName("secondary")}
                  >
                    {copied === "link" ? t("copied") : t("copyLink")}
                  </button>
                </>
              )}
              <button type="button" onClick={async () => await copy(caption, "caption")} className={buttonClassName("secondary")}>
                {copied === "caption" ? t("copied") : t("copyCaption")}
              </button>
            </div>
            {actionError ? <p className="text-sm text-crit-ink">{t("error")}</p> : null}

            <p className="rounded-md bg-muted-wash px-3 py-2.5 text-sm text-gray-700 dark:text-gray-300">{t("privacy")}</p>
          </>
        ) : null}
      </div>
    </Modal>
  );
}
