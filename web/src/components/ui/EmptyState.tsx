import type { ReactNode } from "react";

/**
 * A list with nothing in it, said properly: what the empty space means and where the first item
 * comes from. Before 2026-09-13 most lists showed one grey sentence and no way forward; a brand-new
 * account met four of them in a row.
 *
 * The dashed border is the shared signal for "nothing here yet" — the dashboard's empty state and
 * the CV page already used it, so the lists join them rather than introducing a third look.
 */
export function EmptyState({
  title,
  body,
  actions,
  className = "",
}: {
  title: string;
  body?: string;
  /** Links or buttons, already styled — the state does not decide what they look like. */
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`flex flex-col items-start gap-4 rounded-xl border border-dashed border-gray-300 bg-white p-8 dark:border-gray-700 dark:bg-gray-900 ${className}`}
    >
      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{title}</h2>
        {body ? <p className="max-w-[52ch] text-sm text-gray-600 dark:text-gray-400">{body}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap gap-3">{actions}</div> : null}
    </div>
  );
}
