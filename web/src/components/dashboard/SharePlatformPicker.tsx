"use client";

import { useId } from "react";
import { SocialIcon } from "@/components/layout/SocialIcon";
import { BRAND_GRADIENT_STOPS, PLATFORM_STYLE } from "@/lib/flowCard/platformStyle";
import { PLATFORM_NAME, SHARE_PLATFORMS, type SharePlatform } from "@/lib/flowCard/sharePlan";

/**
 * The share dialog's "where are you posting?" row: one round button per network, as a phone's
 * share sheet draws them (canvas page "C · iki temada", board "Önerilen"). Unpicked: a neutral
 * circle on the theme's own surface with the brand mark in its colour. Picked: the circle takes the
 * brand fill and the site's accent ring, the name under it the accent ink. Everything that is not a
 * brand colour is a theme token, so the row reads the same in light and dark.
 */
export function SharePlatformPicker({
  value,
  onChange,
}: {
  value: SharePlatform;
  onChange: (platform: SharePlatform) => void;
}) {
  // Instagram's mark is a gradient stroke, which needs its own <defs> id on the page.
  const gradientId = `ig-${useId().replace(/:/g, "")}`;

  return (
    <div className="flex flex-wrap gap-3">
      <svg width="0" height="0" className="absolute" aria-hidden="true">
        <defs>
          <linearGradient id={gradientId} x1="0" y1="1" x2="1" y2="0">
            {BRAND_GRADIENT_STOPS.map((color, index) => (
              <stop key={color} offset={index / (BRAND_GRADIENT_STOPS.length - 1)} stopColor={color} />
            ))}
          </linearGradient>
        </defs>
      </svg>

      {SHARE_PLATFORMS.map((platform) => {
        const picked = platform === value;
        const style = PLATFORM_STYLE[platform];
        const paint =
          picked || style.mark === "ink" ? "currentColor" : style.mark === "gradient" ? `url(#${gradientId})` : style.mark;

        return (
          <button
            key={platform}
            type="button"
            aria-pressed={picked}
            onClick={() => onChange(platform)}
            className="group flex w-18 flex-col items-center gap-1.5 rounded-lg py-1 text-xs focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <span
              className={`flex h-12 w-12 items-center justify-center rounded-full transition-colors ${
                picked
                  ? `${style.picked} ring-2 ring-accent ring-offset-2 ring-offset-white dark:ring-offset-gray-900`
                  : "border border-gray-200 bg-muted-wash text-gray-900 group-hover:border-gray-400 dark:border-gray-700 dark:text-gray-100 dark:group-hover:border-gray-500"
              }`}
            >
              <SocialIcon network={platform} className="h-6 w-6" paint={paint} />
            </span>
            <span className={picked ? "font-semibold text-accent-ink" : "text-gray-600 dark:text-gray-400"}>
              {PLATFORM_NAME[platform]}
            </span>
          </button>
        );
      })}
    </div>
  );
}
