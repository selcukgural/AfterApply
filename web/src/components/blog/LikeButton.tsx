"use client";

import { useTranslations } from "next-intl";
import { useAuth } from "@/lib/auth/AuthContext";
import { blogApi } from "@/lib/api/blog";
import { VotePill } from "@/components/blog/VotePill";

interface LikeButtonProps {
  postId: string;
  initialCount: number;
  /** Null for an anonymous render — the page is fetched without the reader's token, so this is null for everyone on the live page. */
  initialLiked: boolean | null;
  /** Where a signed-out reader goes when they click: sign in, then back here. */
  signInHref: string;
}

/**
 * The like on a post: `VotePill` with the post's words and the post's two calls. The pill asks
 * `GET …/like` once as the signed-in reader before it lets a click through, because the page
 * arrived not knowing whether they already liked it (2026-09-21).
 */
export function LikeButton({ postId, initialCount, initialLiked, signInHref }: LikeButtonProps) {
  const t = useTranslations("blog");
  const { isAuthenticated } = useAuth();

  return (
    <VotePill
      icon="heart"
      on={initialLiked}
      count={initialCount}
      load={{ key: ["post", postId], fetch: () => blogApi.likeState(postId) }}
      toggle={() => blogApi.toggleLike(postId)}
      isAuthenticated={isAuthenticated}
      signInHref={signInHref}
      hideZero
      labels={{
        off: t("like"),
        on: t("liked"),
        remove: t("unlike"),
        count: (count) => t("likeCount", { count }),
        signIn: t("signInToLike"),
        error: t("likeError"),
      }}
    />
  );
}
