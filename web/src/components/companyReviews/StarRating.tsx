/**
 * Five stars, read-only. Fractional values fill the star partially (a 3.6 shows three and a bit),
 * which is how an average reads honestly — rounding it to whole stars would print 4.0 and 3.6 the
 * same way. The accessible name carries the number; the stars are decoration for sighted readers.
 */
export function StarRating({
  value,
  label,
  size = "sm",
}: {
  value: number;
  label: string;
  size?: "sm" | "lg";
}) {
  const dimension = size === "lg" ? "h-6 w-6" : "h-4 w-4";
  return (
    <span role="img" aria-label={label} className="inline-flex items-center gap-0.5">
      {[1, 2, 3, 4, 5].map((star) => {
        const fill = Math.max(0, Math.min(1, value - (star - 1)));
        return (
          <span key={star} className={`relative inline-block ${dimension}`} aria-hidden="true">
            <StarGlyph className={`absolute inset-0 ${dimension} text-gray-300 dark:text-gray-700`} />
            <span className="absolute inset-0 overflow-hidden" style={{ width: `${fill * 100}%` }}>
              <StarGlyph className={`${dimension} text-amber-500`} />
            </span>
          </span>
        );
      })}
    </span>
  );
}

export function StarGlyph({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 20 20" className={className} fill="currentColor" aria-hidden="true">
      <path d="M10 1.5l2.6 5.4 5.9.8-4.3 4.1 1.1 5.9L10 14.8l-5.3 2.9 1.1-5.9L1.5 7.7l5.9-.8L10 1.5z" />
    </svg>
  );
}
