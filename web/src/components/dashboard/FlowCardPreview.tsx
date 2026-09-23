"use client";

import { useLayoutEffect, useRef, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { FlowOgCard } from "@/components/seo/FlowOgCard";
import type { FlowCard } from "@/lib/flowCard/card";
import { flowCardText } from "@/lib/flowCard/cardText";
import { FLOW_FORMAT_SPECS, type FlowFormat } from "@/lib/flowCard/formats";
import { previewScale } from "@/lib/flowCard/preview";

// The same brand mark the route embeds, served as a plain file here.
const LOGO_SRC = "/brand/logo-mark-og.png";

/**
 * The share dialog's preview (2026-09-23 canvas "Akış kartı · yükleniyor animasyonu", variant D).
 *
 * The PNG takes a few seconds to render on the server, and the box used to sit empty meanwhile —
 * again on every change of platform, output or period. Now the card is drawn in the browser at
 * once from the same component and text the route uses (FlowOgCard + flowCardText), faded and
 * breathing, with a "preparing" pill; the PNG fades in over it when it has loaded. What the person
 * reads first is therefore already the real card, and the image that replaces it is identical.
 *
 * Both layers occupy the same rectangle (previewScale, the fit `object-contain` would give), so
 * nothing moves when the image arrives. A failed image leaves the draft in place with a retry.
 * Motion stops under prefers-reduced-motion (globals.css); the pill is a live status either way.
 */
export function FlowCardPreview({
  card,
  format,
  imagePath,
  alt,
}: {
  card: FlowCard;
  format: FlowFormat;
  imagePath: string;
  alt: string;
}) {
  const t = useTranslations("flowCard.share");
  const tCard = useTranslations("flowCard.card");
  const locale = useLocale();

  const boxRef = useRef<HTMLDivElement>(null);
  const [box, setBox] = useState({ width: 0, height: 0 });
  // Measured before paint, so the draft never shows at the wrong size for a frame.
  useLayoutEffect(() => {
    const element = boxRef.current;
    if (!element) return;
    const measure = () => setBox({ width: element.clientWidth, height: element.clientHeight });
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  // Keyed by the image's address (plus a retry count): a new platform, output or period is a new
  // address, so the draft comes back by itself without anything having to reset it.
  const [attempt, setAttempt] = useState(0);
  const imageKey = `${imagePath}#${attempt}`;
  const [loadedKey, setLoadedKey] = useState<string | null>(null);
  const [failedKey, setFailedKey] = useState<string | null>(null);
  const loaded = loadedKey === imageKey;
  const failed = failedKey === imageKey;

  const spec = FLOW_FORMAT_SPECS[format];
  const scale = previewScale(box, format);

  return (
    <div ref={boxRef} className="relative flex h-full w-full items-center justify-center">
      {scale > 0 ? (
        <div
          aria-busy={!loaded}
          className="relative overflow-hidden rounded border border-gray-200 bg-white dark:border-gray-700"
          style={{ width: Math.floor(spec.width * scale), height: Math.floor(spec.height * scale) }}
        >
          {!loaded ? (
            <div aria-hidden="true" className={failed ? "opacity-60" : "aa-breathe"}>
              <div style={{ width: spec.width, height: spec.height, transform: `scale(${scale})`, transformOrigin: "top left" }}>
                <FlowOgCard
                  format={format}
                  counts={card.counts}
                  logoSrc={LOGO_SRC}
                  fontFamily="var(--font-geist-sans)"
                  {...flowCardText(card, locale, tCard)}
                />
              </div>
            </div>
          ) : null}
          {!loaded && !failed ? <div aria-hidden="true" className="aa-sweep absolute inset-0" /> : null}

          {/* eslint-disable-next-line @next/next/no-img-element -- a generated PNG from our own route */}
          <img
            key={imageKey}
            src={imagePath}
            alt={alt}
            onLoad={() => setLoadedKey(imageKey)}
            onError={() => setFailedKey(imageKey)}
            className={`absolute inset-0 h-full w-full transition-opacity duration-300 ${loaded ? "opacity-100" : "opacity-0"}`}
          />
        </div>
      ) : null}

      {!loaded ? (
        <div className="absolute bottom-1 left-1/2 flex -translate-x-1/2 items-center gap-2 whitespace-nowrap rounded-full bg-gray-900/90 px-3 py-1 text-xs font-medium text-white">
          {failed ? (
            <>
              <span role="status">{t("imageError")}</span>
              <button type="button" onClick={() => setAttempt((count) => count + 1)} className="underline underline-offset-2">
                {t("retry")}
              </button>
            </>
          ) : (
            <>
              <span aria-hidden="true" className="aa-blink h-1.5 w-1.5 rounded-full bg-blue-300" />
              <span role="status">{t("preparing")}</span>
            </>
          )}
        </div>
      ) : null}
    </div>
  );
}
