import type { ReactNode } from "react";

/**
 * The one card surface every dashboard panel sits on. Kept in a single place so border, radius
 * and padding stay identical across the bento grid — a row of cards that disagree on any of the
 * three reads as several unrelated widgets rather than one board.
 */
export function Card({ className = "", children }: { className?: string; children: ReactNode }) {
  return (
    <div
      className={`rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900 ${className}`}
    >
      {children}
    </div>
  );
}

export function CardHeader({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="mb-3 flex items-baseline justify-between gap-3">
      <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">{title}</h3>
      {hint ? <span className="text-xs text-gray-500 dark:text-gray-400">{hint}</span> : null}
    </div>
  );
}
