"use client";

import { useId, useState } from "react";
import { useTranslations } from "next-intl";
import { StarGlyph } from "@/components/companyReviews/StarRating";

/**
 * One rating question: five stars as a radio group. Arrow keys move the selection the way a native
 * radio group does, so a keyboard user rates without a mouse; the hover preview is for the mouse.
 */
export function RatingInput({
  label,
  value,
  onChange,
  error,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  error?: string;
}) {
  const t = useTranslations("companyReviews.form");
  const [hovered, setHovered] = useState(0);
  const labelId = useId();

  const shown = hovered || value;

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "ArrowRight" || event.key === "ArrowUp") {
      event.preventDefault();
      onChange(Math.min(5, (value || 0) + 1));
    } else if (event.key === "ArrowLeft" || event.key === "ArrowDown") {
      event.preventDefault();
      onChange(Math.max(1, (value || 1) - 1));
    }
  };

  return (
    <div className="flex flex-col gap-1">
      <span id={labelId} className="text-sm font-medium text-gray-700 dark:text-gray-300">
        {label}
      </span>
      <div
        role="radiogroup"
        aria-labelledby={labelId}
        className="flex items-center gap-1"
        onMouseLeave={() => setHovered(0)}
        onKeyDown={handleKeyDown}
      >
        {[1, 2, 3, 4, 5].map((star) => (
          <button
            key={star}
            type="button"
            role="radio"
            aria-checked={value === star}
            aria-label={t("starLabel", { count: star })}
            tabIndex={value === star || (value === 0 && star === 1) ? 0 : -1}
            onMouseEnter={() => setHovered(star)}
            onFocus={() => setHovered(0)}
            onClick={() => onChange(star)}
            className="rounded p-0.5 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <StarGlyph
              className={`h-7 w-7 transition-colors ${
                star <= shown ? "text-amber-500" : "text-gray-300 dark:text-gray-700"
              }`}
            />
          </button>
        ))}
        <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
          {value > 0 ? t("ratingValue", { value }) : t("ratingNone")}
        </span>
      </div>
      {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}
