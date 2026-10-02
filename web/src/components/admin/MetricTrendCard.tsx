"use client";

import { useId, useState, type KeyboardEvent, type PointerEvent } from "react";
import { Card } from "@/components/dashboard/Card";
import { latestPoint, nearestIndex, sevenDayChange, type TrendPoint } from "@/lib/admin/metricTrends";

const VIEW_WIDTH = 320;
const VIEW_HEIGHT = 72;
const PADDING = 6;

/**
 * One product metric's last 30 days on /admin/metrics: today's value, the change over a week and a
 * line you can read day by day (2026-10-02, canvas option B). It replaced the one-row-per-day table
 * as the first thing on the page because the table grew by a row every day and answered "is it
 * going up" only after reading a column top to bottom; the table is still one click away.
 *
 * The hover target is the whole chart, snapped to the nearest day — nobody has to land on a 3px
 * dot — and the same day can be walked with the arrow keys. While a day is picked, the big number
 * and the label under it show that day instead of today, so the tooltip is never the only place
 * the value appears.
 */
export function MetricTrendCard({
  label,
  series,
  formatValue,
  formatChange,
  formatDay,
  changeLabel,
  emptyLabel,
}: {
  label: string;
  series: TrendPoint[];
  formatValue: (value: number) => string;
  formatChange: (change: number) => string;
  formatDay: (isoDate: string) => string;
  /** "7 günde {change}" — receives the formatted change. */
  changeLabel: (change: string) => string;
  emptyLabel: string;
}) {
  const [active, setActive] = useState<number | null>(null);
  const tooltipId = useId();

  const latest = latestPoint(series);
  const change = sevenDayChange(series);
  const picked = active === null ? null : series[active];
  const shown = picked ?? latest;

  const values = series.map((p) => p.value).filter((v): v is number => v !== null);
  const min = values.length ? Math.min(...values) : 0;
  const max = values.length ? Math.max(...values) : 0;
  const step = series.length > 1 ? (VIEW_WIDTH - 2 * PADDING) / (series.length - 1) : 0;
  const x = (index: number) => PADDING + index * step;
  // A flat run sits in the middle rather than dividing by zero.
  const y = (value: number) =>
    max === min
      ? VIEW_HEIGHT / 2
      : VIEW_HEIGHT - PADDING - ((value - min) / (max - min)) * (VIEW_HEIGHT - 2 * PADDING);

  // A day with no value breaks the line instead of dropping it to zero: a retention rate whose
  // cohort was not old enough yet is "unknown", not 0%.
  let line = "";
  let penDown = false;
  series.forEach((point, index) => {
    if (point.value === null) {
      penDown = false;
      return;
    }
    line += `${penDown ? "L" : "M"}${x(index).toFixed(1)} ${y(point.value).toFixed(1)} `;
    penDown = true;
  });

  const pick = (event: PointerEvent<SVGSVGElement>) => {
    const box = event.currentTarget.getBoundingClientRect();
    if (box.width === 0) return;
    // The drawn line starts PADDING units in from each edge; map the pointer onto that span.
    const units = ((event.clientX - box.left) / box.width) * VIEW_WIDTH;
    setActive(nearestIndex((units - PADDING) / (VIEW_WIDTH - 2 * PADDING), series.length));
  };

  const onKeyDown = (event: KeyboardEvent<SVGSVGElement>) => {
    const last = series.length - 1;
    const from = active ?? last;
    const next =
      event.key === "ArrowLeft" ? Math.max(0, from - 1)
      : event.key === "ArrowRight" ? Math.min(last, from + 1)
      : event.key === "Home" ? 0
      : event.key === "End" ? last
      : null;
    if (next === null) {
      if (event.key === "Escape") setActive(null);
      return;
    }
    event.preventDefault();
    setActive(next);
  };

  const activeValue = picked?.value ?? null;
  const tooltipLeft = active === null ? 0 : (x(active) / VIEW_WIDTH) * 100;

  return (
    <Card className="flex flex-col gap-2">
      <span className="text-xs text-gray-500 dark:text-gray-400">{label}</span>
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-2xl font-semibold text-gray-900 tabular-nums dark:text-gray-100">
          {shown?.value == null ? "—" : formatValue(shown.value)}
        </span>
        <span className="text-xs text-gray-500 tabular-nums dark:text-gray-400">
          {picked
            ? formatDay(picked.date)
            : change === null
              ? emptyLabel
              : changeLabel(formatChange(change))}
        </span>
      </div>

      {values.length < 2 ? (
        <p className="py-6 text-center text-xs text-gray-500 dark:text-gray-400">{emptyLabel}</p>
      ) : (
        <div className="relative">
          <svg
            viewBox={`0 0 ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
            className="w-full cursor-crosshair touch-none rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-accent"
            role="img"
            aria-label={label}
            aria-describedby={active === null ? undefined : tooltipId}
            tabIndex={0}
            onPointerMove={pick}
            onPointerDown={pick}
            onPointerLeave={() => setActive(null)}
            onKeyDown={onKeyDown}
            onBlur={() => setActive(null)}
          >
            <path d={line} fill="none" stroke="var(--accent)" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
            {active !== null ? (
              <>
                <line
                  x1={x(active)}
                  x2={x(active)}
                  y1={0}
                  y2={VIEW_HEIGHT}
                  stroke="currentColor"
                  className="text-gray-300 dark:text-gray-700"
                  strokeWidth="1"
                  strokeDasharray="3 3"
                />
                {activeValue !== null ? (
                  <circle
                    cx={x(active)}
                    cy={y(activeValue)}
                    r="4"
                    fill="var(--accent)"
                    stroke="var(--card-surface)"
                    strokeWidth="2"
                  />
                ) : null}
              </>
            ) : latest ? (
              <circle
                cx={x(series.lastIndexOf(latest))}
                cy={y(latest.value!)}
                r="3.5"
                fill="var(--accent)"
                stroke="var(--card-surface)"
                strokeWidth="2"
              />
            ) : null}
          </svg>

          {picked ? (
            <div
              id={tooltipId}
              role="status"
              className="pointer-events-none absolute -top-2 z-10 rounded-md bg-gray-900 px-2 py-1 text-xs whitespace-nowrap text-white shadow-sm dark:bg-gray-100 dark:text-gray-900"
              style={{
                left: `${tooltipLeft}%`,
                // Keep the bubble inside the card at both ends of the line.
                transform: `translate(${tooltipLeft < 15 ? "0" : tooltipLeft > 85 ? "-100%" : "-50%"}, -100%)`,
              }}
            >
              <span className="text-gray-300 dark:text-gray-600">{formatDay(picked.date)}</span>{" "}
              <span className="font-medium tabular-nums">{picked.value === null ? "—" : formatValue(picked.value)}</span>
            </div>
          ) : null}

          <div className="mt-1 flex justify-between text-[11px] text-gray-500 tabular-nums dark:text-gray-400">
            <span>{formatDay(series[0].date)}</span>
            <span>
              {formatValue(min)} – {formatValue(max)}
            </span>
            <span>{formatDay(series[series.length - 1].date)}</span>
          </div>
        </div>
      )}
    </Card>
  );
}
