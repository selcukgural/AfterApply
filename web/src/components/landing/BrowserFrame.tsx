import type { ReactNode } from "react";
import { LogoMark } from "@/components/layout/Logo";

/**
 * A browser window drawn in markup — title-bar dots, a URL pill, the extension's icon at the end of
 * the toolbar — for the landing page to put a page mock inside.
 *
 * Markup rather than a screenshot because that is the landing page's rule for every visual: a
 * component cannot fall behind the product, and this one re-themes with the rest of the page.
 * Decorative by nature; callers wrap it in the `role="img"` description the reader actually gets.
 */
export function BrowserFrame({ url, children, className = "" }: { url: string; children: ReactNode; className?: string }) {
  return (
    <div
      className={`overflow-hidden rounded-xl border border-gray-200 bg-white shadow-xl dark:border-gray-800 dark:bg-gray-900 ${className}`}
    >
      <div className="flex items-center gap-3 border-b border-gray-200 bg-gray-100 px-4 py-2.5 dark:border-gray-800 dark:bg-gray-950">
        <div className="flex gap-1.5">
          <span className="h-2.5 w-2.5 rounded-full bg-gray-300 dark:bg-gray-700" />
          <span className="h-2.5 w-2.5 rounded-full bg-gray-300 dark:bg-gray-700" />
          <span className="h-2.5 w-2.5 rounded-full bg-gray-300 dark:bg-gray-700" />
        </div>
        <div className="flex h-6 min-w-0 flex-1 items-center gap-1.5 rounded-full border border-gray-200 bg-white px-3 text-xs text-gray-500 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-400">
          <svg viewBox="0 0 24 24" className="h-3 w-3 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z"
            />
          </svg>
          <span className="truncate">{url}</span>
        </div>
        <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md border border-accent/50 bg-white dark:bg-gray-900">
          <LogoMark className="h-3.5 w-3.5" />
        </span>
      </div>
      <div className="relative">{children}</div>
    </div>
  );
}
