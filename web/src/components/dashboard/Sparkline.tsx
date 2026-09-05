const VIEW_WIDTH = 320;
const VIEW_HEIGHT = 64;
const PADDING = 5;

/**
 * A 12-point trend line. Hand-rolled SVG rather than a chart library: at this size the whole
 * mark is one path, one wash and one end dot, and the library would cost more than it draws.
 *
 * Scales uniformly (no preserveAspectRatio="none"), so the 2px stroke and the end dot keep their
 * shape at any container width.
 */
export function Sparkline({ points, ariaLabel }: { points: number[]; ariaLabel: string }) {
  if (points.length < 2) {
    return null;
  }

  const max = Math.max(...points);
  const step = (VIEW_WIDTH - 2 * PADDING) / (points.length - 1);
  // A flat run of equal values (all zero included) sits on the baseline instead of dividing by 0.
  const y = (value: number) =>
    max <= 0 ? VIEW_HEIGHT - PADDING : VIEW_HEIGHT - PADDING - (value / max) * (VIEW_HEIGHT - 2 * PADDING);

  const coords = points.map((value, index) => [PADDING + index * step, y(value)] as const);
  const line = coords.map(([x, py], index) => `${index === 0 ? "M" : "L"}${x.toFixed(1)} ${py.toFixed(1)}`).join(" ");
  const [lastX, lastY] = coords[coords.length - 1];

  return (
    <svg
      viewBox={`0 0 ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
      className="w-full"
      role="img"
      aria-label={ariaLabel}
    >
      <path
        d={`${line} L${lastX.toFixed(1)} ${VIEW_HEIGHT} L${PADDING} ${VIEW_HEIGHT} Z`}
        fill="var(--accent)"
        opacity="0.12"
      />
      <path d={line} fill="none" stroke="var(--accent)" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
      {/* Surface ring keeps the end dot legible where it sits on top of the line. */}
      <circle cx={lastX} cy={lastY} r="4" fill="var(--accent)" stroke="var(--card-surface)" strokeWidth="2" />
    </svg>
  );
}
