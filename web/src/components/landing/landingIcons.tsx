/**
 * The outline icon paths the landing page draws inline (there is no icon library in this app).
 * Shared between the feature grid (a server component) and the tools strip (a client component),
 * which is why they live in a file of their own: a client component cannot import from a module
 * that also pulls in `next-intl/server`.
 */
export const LANDING_ICON_PATHS = {
  tracking: (
    <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m-9 4h12a2 2 0 002-2V6a2 2 0 00-2-2H8.5L5 7.5V18a2 2 0 002 2z" />
  ),
  timeline: <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />,
  analytics: <path strokeLinecap="round" strokeLinejoin="round" d="M3 3v18h18M8 17V9m5 8V5m5 12v-6" />,
  noResponse: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
    />
  ),
  extension: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M10.5 5.25a2.25 2.25 0 114.5 0V6h2.25a1.5 1.5 0 011.5 1.5V10h.75a2.25 2.25 0 110 4.5H18.75v2.25a1.5 1.5 0 01-1.5 1.5H15v.75a2.25 2.25 0 11-4.5 0V18H8.25a1.5 1.5 0 01-1.5-1.5V14.25H6a2.25 2.25 0 110-4.5h.75V7.5A1.5 1.5 0 018.25 6h2.25v-.75z"
    />
  ),
  cv: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M9 12.75h4.5m-4.5 3h6m3-10.5v13.5a1.5 1.5 0 01-1.5 1.5h-9a1.5 1.5 0 01-1.5-1.5V5.25a1.5 1.5 0 011.5-1.5h5.379a1.5 1.5 0 011.06.44l3.122 3.12a1.5 1.5 0 01.439 1.061z"
    />
  ),
  check: <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />,
  companies: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M3 21h18M5 21V5.5A1.5 1.5 0 016.5 4h7A1.5 1.5 0 0115 5.5V21M15 10h3.5A1.5 1.5 0 0120 11.5V21M8 8h3M8 11.5h3M8 15h3M18 14h.01M18 17.5h.01"
    />
  ),
} as const;

export type LandingIcon = keyof typeof LANDING_ICON_PATHS;

export function LandingIcon({ name, className = "h-5 w-5" }: { name: LandingIcon; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.75} aria-hidden="true">
      {LANDING_ICON_PATHS[name]}
    </svg>
  );
}
