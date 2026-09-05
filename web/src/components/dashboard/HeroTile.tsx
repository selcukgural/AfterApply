import { Sparkline } from "@/components/dashboard/Sparkline";

/**
 * The single number the dashboard leads with, plus the trend behind it. Exactly one of these per
 * view — the size is what makes it the entry point, and a second one would cancel the first.
 */
export function HeroTile({
  label,
  value,
  sub,
  trend,
  trendLabel,
  trendAriaLabel,
}: {
  label: string;
  value: string;
  sub: string;
  trend: number[];
  trendLabel: string;
  trendAriaLabel: string;
}) {
  return (
    <div className="flex flex-col justify-between gap-6 rounded-xl border border-gray-200 bg-[linear-gradient(160deg,var(--accent-wash),var(--card-surface)_65%)] p-5 dark:border-gray-800">
      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-gray-600 dark:text-gray-300">{label}</span>
        {/* Proportional figures, not tabular: at 48px+ tabular-nums makes a number like 1.181 look loose. */}
        <span className="text-5xl font-bold tracking-tighter text-gray-900 dark:text-gray-100">{value}</span>
        <p className="mt-2 max-w-[34ch] text-sm text-gray-600 dark:text-gray-400">{sub}</p>
      </div>
      {trend.length > 1 ? (
        <div className="flex flex-col gap-1.5">
          <span className="text-xs text-gray-500 dark:text-gray-400">{trendLabel}</span>
          <Sparkline points={trend} ariaLabel={trendAriaLabel} />
        </div>
      ) : null}
    </div>
  );
}
