import { Link } from "@/i18n/navigation";

/**
 * What a page says in place of its feature when an admin has switched that feature off
 * (runtime flags, DECISIONS.md 2026-09-27): a plain statement and a way back, instead of a form
 * whose request would fail or a table that shows its error message.
 */
export function FeatureOffNotice({ title, body, backLabel }: { title: string; body: string; backLabel: string }) {
  return (
    <section className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50 p-6 dark:border-gray-800 dark:bg-gray-900">
      <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{title}</h2>
      <p className="max-w-[62ch] text-sm leading-relaxed text-gray-600 dark:text-gray-400">{body}</p>
      <Link href="/" className="mt-1 w-fit text-sm font-medium text-accent-ink underline-offset-2 hover:underline">
        {backLabel}
      </Link>
    </section>
  );
}
