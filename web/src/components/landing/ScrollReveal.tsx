"use client";

import { useEffect, useRef, useState } from "react";

/**
 * Fades a section in as it scrolls into view — but only ever *after* the page has shown it.
 *
 * The server renders every section visible. Until 2026-09-14 it did the opposite: the SSR output
 * carried `opacity-0` on eleven sections and relied on hydration plus an IntersectionObserver to
 * turn them on, so on a phone that took 4–5 s to become interactive, anyone scrolling right away
 * met a blank page below the hero — and a crawler's render had the same gap. Now, on mount, a
 * section that is still below the viewport is hidden and observed; one already on screen is left
 * alone. Hiding something the visitor cannot yet see is the only moment the swap is invisible.
 *
 * Skipped entirely under prefers-reduced-motion or without IntersectionObserver (spec §27/§29).
 */
export function ScrollReveal({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (typeof IntersectionObserver === "undefined" || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      return;
    }
    // Already on screen (or above it): leave it as rendered. Only what is still to come animates.
    if (node.getBoundingClientRect().top < window.innerHeight) return;

    setVisible(false);
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setVisible(true);
          observer.disconnect();
        }
      },
      { threshold: 0.15 },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      className={`transition-all duration-700 ease-out ${visible ? "translate-y-0 opacity-100" : "translate-y-4 opacity-0"} ${className}`}
    >
      {children}
    </div>
  );
}
