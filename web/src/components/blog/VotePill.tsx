"use client";

import { useReducer, useState, type ReactNode } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/httpClient";
import { initialVoteState, voteReducer, type VoteSnapshot } from "@/lib/blog/vote";

export interface VotePillLabels {
  /** The pill's text before the reader voted ("Beğen"). */
  off: string;
  /** Its text once they have ("Beğendin") — the reader sees they already did, and does not press again to "make sure". */
  on: string;
  /** The hover hint on a voted pill: what a click does now ("Beğeniyi kaldır"). */
  remove: string;
  /** The count, spelled out for a screen reader. */
  count: (count: number) => string;
  /** A signed-out reader's tooltip on the link to sign in. */
  signIn: string;
  /** Shown under the pill when the toggle failed and the API sent no message of its own. */
  error: string;
}

interface VotePillProps {
  icon: "heart" | "thumb";
  /** Whether this reader voted, as the server said when the item was fetched; null when it was fetched without a token. */
  on: boolean | null;
  count: number;
  /**
   * Asks the server for this reader's vote when `on` arrived null and the reader is signed in.
   * Leave it out when the parent fetches the item again with the token itself (the comment
   * list does) — the pill then just waits for `on` to become a boolean.
   */
  load?: { key: readonly unknown[]; fetch: () => Promise<VoteSnapshot> };
  toggle: () => Promise<VoteSnapshot>;
  isAuthenticated: boolean;
  /** Where a signed-out reader goes when they click: sign in, then back here. */
  signInHref: string;
  labels: VotePillLabels;
  /** The like on a post hides "0"; a comment's helpful count shows it. */
  hideZero?: boolean;
}

/**
 * The one toggle a reader has — "like" on a post, "helpful" on a comment — drawn as the review
 * card's pill. The pill does not let a click through until it knows whether this reader already
 * voted (`lib/blog/vote.ts` has the rule and the reason); until then the count is a shimmer and
 * the pill is inert. A voted pill says so in words and, on hover, that a click removes the vote.
 * A signed-out reader sees the count and is sent to sign in — no anonymous votes, so there is
 * no fingerprint, no cookie and no IP to store (DECISIONS.md 2026-09-19).
 */
export function VotePill({ icon, on, count, load, toggle, isAuthenticated, signInHref, labels, hideZero = false }: VotePillProps) {
  const [state, dispatch] = useReducer(voteReducer, initialVoteState(on, count));
  const [error, setError] = useState<string | null>(null);

  // The post page arrives without the reader's token: the pill asks once, as them.
  const asked = useQuery({
    queryKey: ["blog", "vote", ...(load?.key ?? [])],
    queryFn: () => load!.fetch(),
    enabled: isAuthenticated && on === null && load !== undefined,
    staleTime: Number.POSITIVE_INFINITY,
    refetchOnWindowFocus: false,
  });
  // A fresh item (the comment list re-read as the signed-in reader) carries a fresh vote: the
  // pill follows it — the "adjust state on a prop change" pattern, a render-time reset, not an effect.
  const [seen, setSeen] = useState({ on, count });
  if (seen.on !== on || seen.count !== count) {
    setSeen({ on, count });
    dispatch({ type: "known", on: on ?? asked.data?.on ?? null, count });
  }

  const [seenAnswer, setSeenAnswer] = useState<VoteSnapshot | undefined>(undefined);
  if (asked.data !== undefined && asked.data !== seenAnswer) {
    setSeenAnswer(asked.data);
    dispatch({ type: "known", on: asked.data.on, count: asked.data.count });
  }

  const mutation = useMutation({
    mutationFn: toggle,
    onMutate: () => {
      setError(null);
      dispatch({ type: "click" });
    },
    onSuccess: (result) => dispatch({ type: "settled", on: result.on, count: result.count }),
    onError: (err) => {
      dispatch({ type: "failed" });
      setError(err instanceof ApiError && err.message ? err.message : labels.error);
    },
  });

  const voted = state.on === true;
  const unknown = isAuthenticated && state.on === null;
  const heart = icon === "heart";

  // The unvoted pill's hover invites the press: the heart fills with the theme's red (--crit,
  // light and dark values) and grows a touch; the thumb only darkens its border. The voted pill
  // hovers the other way — the icon empties and the hint says the click removes the vote — so
  // the two states cannot be mistaken for one another.
  const className = `group relative inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition-colors disabled:opacity-60 ${
    voted
      ? "border-accent bg-accent/10 text-accent-ink hover:border-crit/50"
      : heart
        ? "border-gray-300 text-gray-700 hover:border-crit/40 hover:bg-crit-wash/60 dark:border-gray-700 dark:text-gray-300 dark:hover:border-crit/50 dark:hover:bg-crit-wash/60"
        : "border-gray-300 text-gray-700 hover:border-gray-400 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500"
  }`;
  const iconClassName = `h-3.5 w-3.5 transition-[fill,stroke,transform] duration-150 motion-reduce:transition-none ${
    voted
      ? "group-hover:fill-transparent group-hover:stroke-crit"
      : heart
        ? "group-hover:scale-110 group-hover:fill-crit group-hover:stroke-crit motion-reduce:group-hover:scale-100"
        : ""
  }`;

  const countNode: ReactNode =
    unknown ? (
      <span aria-hidden="true" className="inline-block h-3 w-3 animate-pulse rounded bg-gray-200 motion-reduce:animate-none dark:bg-gray-700" />
    ) : hideZero && state.count === 0 ? null : (
      <span className="tabular-nums opacity-80" aria-label={labels.count(state.count)}>
        {state.count}
      </span>
    );

  const body = (
    <>
      {heart ? <HeartIcon className={iconClassName} filled={voted} /> : <ThumbIcon className={iconClassName} />}
      {voted ? labels.on : labels.off}
      {countNode}
    </>
  );

  return (
    <div className="flex flex-col gap-1">
      {isAuthenticated ? (
        <button
          type="button"
          onClick={() => mutation.mutate()}
          disabled={unknown || state.busy}
          aria-pressed={voted}
          aria-busy={unknown || state.busy}
          className={className}
        >
          {body}
          {voted && (
            <span
              role="tooltip"
              className="pointer-events-none absolute bottom-full left-1/2 mb-1.5 -translate-x-1/2 translate-y-1 whitespace-nowrap rounded-md bg-gray-900 px-2 py-0.5 text-[11px] font-medium text-white opacity-0 transition-[opacity,transform] duration-150 group-hover:translate-y-0 group-hover:opacity-100 group-focus-visible:translate-y-0 group-focus-visible:opacity-100 motion-reduce:transition-none dark:bg-gray-100 dark:text-gray-900"
            >
              {labels.remove}
            </span>
          )}
        </button>
      ) : (
        <Link href={signInHref} className={className} title={labels.signIn}>
          {body}
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

function HeartIcon({ className, filled }: { className: string; filled: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill={filled ? "currentColor" : "none"} stroke="currentColor" strokeWidth={2} aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
    </svg>
  );
}

// The thumb stays an outline when voted — its colour and words carry the state, as on the review cards.
function ThumbIcon({ className }: { className: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M7 10v12" />
      <path d="M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z" />
    </svg>
  );
}
