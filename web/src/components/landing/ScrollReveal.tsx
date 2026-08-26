"use client";

import { useEffect, useRef, useState } from "react";

// Computed lazily (not in an effect) so a reduced-motion or no-IntersectionObserver
// visitor never goes through an invisible-then-visible flash on mount.
function startsRevealed(): boolean {
  if (typeof window === "undefined") return false;
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches || typeof IntersectionObserver === "undefined";
}

// Fades a section in once it scrolls into view. Skips the animation entirely
// under prefers-reduced-motion, per spec §27/§29.
export function ScrollReveal({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(startsRevealed);

  useEffect(() => {
    if (visible) return;
    const node = ref.current;
    if (!node) return;

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
  }, [visible]);

  return (
    <div
      ref={ref}
      className={`transition-all duration-700 ease-out ${visible ? "translate-y-0 opacity-100" : "translate-y-4 opacity-0"} ${className}`}
    >
      {children}
    </div>
  );
}
