import type { ReactNode } from "react";

interface ProgressRingProps {
  /** Outer size in px; the stroke scales with it. */
  size: number;
  /** Whether the arc turns. Off once the page has stopped waiting, and always off under
   * prefers-reduced-motion (the `motion-safe:` variant does that). */
  spinning: boolean;
  /** What sits in the middle — the elapsed time on the result page, nothing on the bounce. */
  children?: ReactNode;
  className?: string;
}

/**
 * The waiting mark the payment pages share: a track in the theme's `--track` and a quarter
 * arc in the logo's blue→teal, turning while there is something to wait for. Drawn inline so
 * both pages — including the chrome-less PayTR bounce, which may import nothing that talks to
 * the API — get the same mark from the same file.
 */
export function ProgressRing({ size, spinning, children, className = "" }: ProgressRingProps) {
  const stroke = Math.max(4, Math.round(size / 16));
  const radius = size / 2 - stroke;
  const circumference = 2 * Math.PI * radius;
  const gradientId = `aa-ring-${size}`;

  return (
    <div className={`relative ${className}`} style={{ width: size, height: size }}>
      <svg
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        aria-hidden="true"
        className={`absolute inset-0 ${spinning ? "motion-safe:animate-[spin_1.4s_linear_infinite]" : ""}`}
      >
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stopColor="#1C39B7" />
            <stop offset="1" stopColor="#15AAB7" />
          </linearGradient>
        </defs>
        <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--track)" strokeWidth={stroke} />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={`url(#${gradientId})`}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={`${circumference * 0.34} ${circumference * 0.66}`}
          transform={`rotate(-90 ${size / 2} ${size / 2})`}
        />
      </svg>
      {children && <div className="absolute inset-0 flex flex-col items-center justify-center">{children}</div>}
    </div>
  );
}
