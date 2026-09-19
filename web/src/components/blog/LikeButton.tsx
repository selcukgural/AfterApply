"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { blogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";

interface LikeButtonProps {
  postId: string;
  initialCount: number;
  /** Null for an anonymous reader — the server only knows for a signed-in one. */
  initialLiked: boolean | null;
  /** Where a signed-out reader goes when they click: sign in, then back here. */
  signInHref: string;
}

/**
 * The review card's "helpful" control, for a post: one toggle per account, the count next to it.
 * A signed-out reader sees the count and is sent to sign in — no anonymous likes, so there is no
 * fingerprint, no cookie and no IP to store (DECISIONS.md 2026-09-19). Optimistic: the count
 * moves on click and is corrected from the server's answer.
 */
export function LikeButton({ postId, initialCount, initialLiked, signInHref }: LikeButtonProps) {
  const t = useTranslations("blog");
  const { isAuthenticated } = useAuth();
  const [liked, setLiked] = useState(initialLiked === true);
  const [count, setCount] = useState(initialCount);
  const [error, setError] = useState<string | null>(null);

  const toggle = useMutation({
    mutationFn: () => blogApi.toggleLike(postId),
    onMutate: () => {
      setError(null);
      setLiked((value) => !value);
      setCount((value) => value + (liked ? -1 : 1));
    },
    onSuccess: (result) => {
      setLiked(result.liked);
      setCount(result.likeCount);
    },
    onError: (err) => {
      setLiked(initialLiked === true);
      setCount(initialCount);
      setError(err instanceof ApiError ? err.message : t("likeError"));
    },
  });

  // On hover the heart fills with the theme's red (--crit, which has a light and a dark value) and
  // grows a touch, so the control reads as "press me" rather than as a label with a count. The
  // pill itself keeps its liked/unliked colours; only the heart answers the hover.
  const className = `group inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors disabled:opacity-60 ${
    liked
      ? "border-accent bg-accent/10 text-accent-ink"
      : "border-gray-300 text-gray-700 hover:border-crit/40 hover:bg-crit-wash/60 dark:border-gray-700 dark:text-gray-300 dark:hover:border-crit/50 dark:hover:bg-crit-wash/60"
  }`;

  const label = (
    <>
      <svg
        viewBox="0 0 24 24"
        className="h-3.5 w-3.5 transition-[fill,stroke,transform] duration-150 group-hover:scale-110 group-hover:fill-crit group-hover:stroke-crit motion-reduce:transition-none motion-reduce:group-hover:scale-100"
        fill={liked ? "currentColor" : "none"}
        stroke="currentColor"
        strokeWidth={2}
        aria-hidden="true"
      >
        <path strokeLinecap="round" strokeLinejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
      </svg>
      {t("like")}
      {count > 0 && (
        <span className="tabular-nums opacity-80" aria-label={t("likeCount", { count })}>
          {count}
        </span>
      )}
    </>
  );

  return (
    <div className="flex flex-col gap-1">
      {isAuthenticated ? (
        <button type="button" onClick={() => toggle.mutate()} disabled={toggle.isPending} aria-pressed={liked} className={className}>
          {label}
        </button>
      ) : (
        <Link href={signInHref} className={className} title={t("signInToLike")}>
          {label}
        </Link>
      )}
      {error && (
        <p role="alert" className="text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </div>
  );
}
