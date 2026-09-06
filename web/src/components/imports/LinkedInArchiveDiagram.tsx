export interface LinkedInArchiveDiagramLabels {
  /** Read out in place of the whole diagram — its parts carry no meaning on their own. */
  a11y: string;
  caption: string;
  heading: string;
  fullOption: string;
  fullEta: string;
  pickOption: string;
  pickEta: string;
  jobs: string;
  otherOption1: string;
  otherOption2: string;
  cta: string;
  pointer: string;
}

function ClockIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-3.5 w-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path strokeLinecap="round" d="M12 7.5V12l3 1.75" />
    </svg>
  );
}

function RadioMark({ selected }: { selected: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className="mt-0.5 h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      {selected && <circle cx="12" cy="12" r="4.5" fill="currentColor" stroke="none" />}
    </svg>
  );
}

function CheckboxMark({ checked }: { checked: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
      <rect x="3.5" y="3.5" width="17" height="17" rx="4" />
      {checked && <path strokeLinecap="round" strokeLinejoin="round" d="M7.5 12.25l3 3 6-6.5" />}
    </svg>
  );
}

/**
 * A deliberately stylised sketch of LinkedIn's "Get a copy of your data" screen — the one place
 * in this flow the user is not looking at our UI, and the one choice on it (the second option,
 * with "Jobs" ticked) that decides whether the archive shows up in ten minutes or tomorrow.
 * Drawn rather than screenshotted on purpose: it carries our own colours and no LinkedIn mark, it
 * survives their next redesign, and every label goes through next-intl instead of being burned
 * into an image. Labels arrive as props so the same sketch renders from the server-side help page
 * and the client-side import page.
 */
export function LinkedInArchiveDiagram({ labels }: { labels: LinkedInArchiveDiagramLabels }) {
  return (
    <figure className="flex flex-col gap-2">
      <div
        role="img"
        aria-label={labels.a11y}
        className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-gray-50 p-4 text-xs dark:border-gray-800 dark:bg-gray-900/60"
      >
        <p className="font-semibold text-gray-700 dark:text-gray-300">{labels.heading}</p>

        <div className="flex items-start gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-gray-400 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-500">
          <RadioMark selected={false} />
          <span className="flex-1 line-through decoration-gray-300 dark:decoration-gray-600">{labels.fullOption}</span>
          <span className="flex items-center gap-1 whitespace-nowrap">
            <ClockIcon />
            {labels.fullEta}
          </span>
        </div>

        <div className="flex flex-col gap-3 rounded-lg border-2 border-accent bg-white px-3 py-2 text-gray-700 dark:bg-gray-900 dark:text-gray-300">
          <div className="flex items-start gap-2">
            <span className="text-accent-ink">
              <RadioMark selected />
            </span>
            <span className="flex-1 font-medium">{labels.pickOption}</span>
            <span className="flex items-center gap-1 whitespace-nowrap font-medium text-accent-ink">
              <ClockIcon />
              {labels.pickEta}
            </span>
          </div>

          <div className="flex flex-wrap items-center gap-2 pl-6">
            <span className="flex items-center gap-1.5 rounded-md border border-gray-200 px-2 py-1 text-gray-400 dark:border-gray-700 dark:text-gray-500">
              <CheckboxMark checked={false} />
              {labels.otherOption1}
            </span>
            <span className="flex items-center gap-1.5 rounded-md border border-gray-200 px-2 py-1 text-gray-400 dark:border-gray-700 dark:text-gray-500">
              <CheckboxMark checked={false} />
              {labels.otherOption2}
            </span>
            <span className="flex items-center gap-1.5 rounded-md border-2 border-accent bg-accent-wash px-2 py-1 font-semibold text-accent-ink">
              <CheckboxMark checked />
              {labels.jobs}
            </span>
            <span className="flex items-center gap-1 font-medium text-accent-ink">
              <span aria-hidden="true">←</span>
              {labels.pointer}
            </span>
          </div>

          <span className="ml-6 self-start rounded-md bg-accent px-3 py-1.5 font-medium text-white">{labels.cta}</span>
        </div>
      </div>
      <figcaption className="text-xs text-gray-500 dark:text-gray-400">{labels.caption}</figcaption>
    </figure>
  );
}
