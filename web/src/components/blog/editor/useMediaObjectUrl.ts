"use client";

import { useEffect, useState } from "react";
import { blogApi } from "@/lib/api/blog";

/**
 * The bytes of a blog image as a blob URL the editor can put in an `<img>`. Needed because an
 * `<img src>` sends no Bearer token, and a draft's images are 404 to anyone but the author — the
 * public page never needs this (its images are public once the post is), only the editor does.
 * Revoked on unmount so the blob does not outlive the view.
 */
export function useMediaObjectUrl(mediaUrl: string | null): { url: string | null; failed: boolean } {
  // Keyed by the media URL it was fetched for, so a stale result for the previous image is never
  // shown for the next one — and nothing has to be reset synchronously when the URL changes.
  const [loaded, setLoaded] = useState<{ mediaUrl: string; url: string | null; failed: boolean } | null>(null);

  useEffect(() => {
    if (!mediaUrl) return;

    let objectUrl: string | null = null;
    let cancelled = false;

    blogApi
      .fetchMediaBlob(mediaUrl)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setLoaded({ mediaUrl, url: objectUrl, failed: false });
      })
      .catch(() => {
        if (!cancelled) setLoaded({ mediaUrl, url: null, failed: true });
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [mediaUrl]);

  return mediaUrl && loaded?.mediaUrl === mediaUrl ? { url: loaded.url, failed: loaded.failed } : { url: null, failed: false };
}
