export interface WeeklyJobsSample {
  title: string;
  company: string;
  score: number;
}

/**
 * Three sample postings with their fit score — the picture of "what a delivered week looks like"
 * that the dashboard announcement and the landing hero share, so the two never drift apart. Pure
 * markup: the captions come from the caller's catalogue slice. Decorative for assistive
 * technology; the surrounding copy says everything the list shows.
 *
 * The scores follow the postings page's own colour rule: 80 and above good, 60–79 accent, and a
 * posting under 80 is dimmed so the ranking reads at a glance.
 */
export function WeeklyJobsSampleList({ label, samples, footer }: { label: string; samples: WeeklyJobsSample[]; footer?: string }) {
  return (
    <div className="flex flex-col gap-2" aria-hidden="true">
      <span className="text-[11px] font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">{label}</span>
      {samples.map((sample) => (
        <div
          key={sample.title}
          className={`flex items-center justify-between gap-3 rounded-lg border border-gray-200 bg-white px-3 py-2.5 dark:border-gray-800 dark:bg-gray-900 ${sample.score < 80 ? "opacity-60" : ""}`}
        >
          <div className="flex min-w-0 flex-col">
            <span className="truncate text-[13px] font-medium text-gray-900 dark:text-gray-100">{sample.title}</span>
            <span className="text-xs text-gray-500 dark:text-gray-400">{sample.company}</span>
          </div>
          <span
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-[3px] text-[13px] font-semibold text-gray-900 dark:text-gray-100 ${
              sample.score >= 80 ? "border-good" : "border-accent"
            }`}
          >
            {sample.score}
          </span>
        </div>
      ))}
      {footer ? <span className="pt-1 text-xs text-gray-500 dark:text-gray-400">{footer}</span> : null}
    </div>
  );
}
