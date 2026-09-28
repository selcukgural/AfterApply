"use client";

import { useState } from "react";

interface AvatarProps {
  /** A same-origin /api/avatars/{id} path (next.config rewrites it to the API), or null/undefined. */
  src: string | null | undefined;
  /** Drawn when there is no photo, or the photo fails to load (a hidden or removed one answers 404). */
  initials: string;
  /** Size, shape and the initials' colours — the call site's own, so each place keeps its look. */
  className: string;
}

/**
 * The profile photo where there is one, the initials where there is not (DECISIONS.md 2026-09-28).
 * Decorative: every place that draws it prints the name beside it, so the image has an empty alt
 * rather than repeating the name to a screen reader.
 */
export function Avatar({ src, initials, className }: AvatarProps) {
  // Keyed on the URL: a new photo is a new id, so a failure of the old one must not stick.
  const [failedSrc, setFailedSrc] = useState<string | null>(null);
  const showPhoto = Boolean(src) && failedSrc !== src;

  return (
    <span aria-hidden="true" className={`flex shrink-0 items-center justify-center overflow-hidden rounded-full ${className}`}>
      {showPhoto ? (
        // eslint-disable-next-line @next/next/no-img-element -- a same-origin 256 px WebP; next/image would only re-encode it
        <img src={src!} alt="" className="h-full w-full object-cover" onError={() => setFailedSrc(src ?? null)} />
      ) : (
        initials
      )}
    </span>
  );
}
