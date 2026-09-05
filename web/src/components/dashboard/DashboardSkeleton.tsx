/**
 * Content-shaped placeholders rather than a "Loading…" line: the blocks occupy the same space
 * the real cards will, so the page does not jump when the two queries land.
 */
function Block({ className = "" }: { className?: string }) {
  return <span className={`aa-skeleton block rounded-md ${className}`} />;
}

function CardShell({ className = "", children }: { className?: string; children?: React.ReactNode }) {
  return (
    <div
      className={`flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900 ${className}`}
    >
      {children}
    </div>
  );
}

export function DashboardSkeleton() {
  return (
    <div className="flex flex-col gap-4" aria-busy="true" aria-live="polite">
      {/* Mirrors the real board's single two-column split, so nothing shifts when data lands. */}
      <div className="grid gap-4 lg:grid-cols-2">
        <CardShell className="justify-between gap-6">
          <div className="flex flex-col gap-3">
            <Block className="h-3 w-24" />
            <Block className="h-12 w-40" />
            <Block className="h-3 w-56" />
          </div>
          <Block className="h-16 w-full" />
        </CardShell>
        <div className="grid grid-cols-2 gap-4">
          {Array.from({ length: 4 }, (_, index) => (
            <CardShell key={index}>
              <Block className="h-3 w-20" />
              <Block className="h-8 w-16" />
              <Block className="h-4 w-24 rounded-full" />
            </CardShell>
          ))}
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <CardShell>
          <Block className="h-3 w-32" />
          {Array.from({ length: 4 }, (_, index) => (
            <Block key={index} className="h-2.5 w-full" />
          ))}
        </CardShell>
        <CardShell>
          <Block className="h-3 w-28" />
          <Block className="h-8 w-32" />
          <Block className="h-3 w-full" />
        </CardShell>
      </div>
    </div>
  );
}
