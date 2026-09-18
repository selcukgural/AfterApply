import type { SocialNetwork } from "@/lib/constants/socialLinks";

/**
 * The three networks' own marks, drawn in `currentColor` so they take the footer's text colour in
 * both themes rather than each brand's own blue/black/gradient — a monochrome mark is what every
 * one of the three allows for exactly this use, and it is the only way three logos sit in one
 * row without shouting. Shapes: LinkedIn's "in" tile, X's letterform, Instagram's glyph
 * (rounded square, lens, flash). No external asset: the CSP would block it, and 24 px of path
 * data is smaller than the request.
 */
export function SocialIcon({ network, className = "h-4 w-4" }: { network: SocialNetwork; className?: string }) {
  switch (network) {
    case "linkedin":
      return (
        <svg viewBox="0 0 24 24" className={className} fill="currentColor" aria-hidden="true">
          <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z" />
        </svg>
      );
    case "x":
      return (
        <svg viewBox="0 0 24 24" className={className} fill="currentColor" aria-hidden="true">
          <path d="M18.901 1.153h3.68l-8.04 9.19L24 22.846h-7.406l-5.8-7.584-6.638 7.584H.474l8.6-9.83L0 1.154h7.594l5.243 6.932ZM17.61 20.644h2.039L6.486 3.24H4.298Z" />
        </svg>
      );
    case "instagram":
      return (
        <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <rect x="2.5" y="2.5" width="19" height="19" rx="5.5" />
          <circle cx="12" cy="12" r="4.25" />
          <circle cx="17.6" cy="6.4" r="1.15" fill="currentColor" stroke="none" />
        </svg>
      );
  }
}
