"use client";

import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { cardAge, type CardTone, sourceGroupOf } from "@/lib/board/board";
import type { BoardCardResponse, BoardCardOrigin } from "@/types/api";
import { CardMenu } from "./CardMenu";
import { CompanyMark } from "./CompanyMark";

const TONE_DOT: Record<CardTone, string> = {
  fresh: "bg-good",
  quiet: "bg-muted",
  silent: "bg-warn",
  stale: "bg-crit",
  neutral: "bg-muted",
};

// Ink as well as dot: the colour must not be the only difference (the text and weight change too).
const TONE_TEXT: Record<CardTone, string> = {
  fresh: "text-gray-600 dark:text-gray-300",
  quiet: "text-gray-600 dark:text-gray-300",
  silent: "font-medium text-warn-ink",
  stale: "font-semibold text-crit-ink",
  neutral: "text-gray-600 dark:text-gray-300",
};

const ORIGIN_PILL: Partial<Record<BoardCardOrigin, string>> = {
  Email: "bg-accent-wash text-accent-ink",
  EmailReturned: "bg-accent-wash text-accent-ink",
  Extension: "bg-muted-wash text-muted-ink",
  Later: "bg-muted-wash text-muted-ink",
};

export interface CardActions {
  onSeen: (card: BoardCardResponse) => void;
  onChangeStage: (card: BoardCardResponse) => void;
  onMoveToTop: (card: BoardCardResponse) => void;
  onRemove: (card: BoardCardResponse) => void;
}

interface BoardCardProps {
  card: BoardCardResponse;
  now: Date;
  silentDays: number;
  actions: CardActions;
}

/** A card as it sits in a column: draggable by its whole body (a handle takes keyboard focus). */
export function SortableBoardCard(props: BoardCardProps) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({
    id: props.card.id,
    data: { card: props.card },
  });

  // Mouse and touch start a drag from anywhere on the card; the keyboard only from the handle —
  // otherwise Enter on the title link would pick the card up instead of opening it. dnd-kit types
  // its listeners as bare Functions, hence the casts.
  const { onKeyDown, ...pointerListeners } = (listeners ?? {}) as Record<string, (event: never) => void>;
  // (onMouseDown and onTouchStart end up on the wrapper.)

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={isDragging ? "opacity-40" : undefined}
      {...(pointerListeners as React.HTMLAttributes<HTMLDivElement>)}
    >
      <BoardCardBody
        {...props}
        handle={{ ref: setActivatorNodeRef, attributes, onKeyDown: onKeyDown as React.KeyboardEventHandler | undefined }}
      />
    </div>
  );
}

interface HandleProps {
  ref?: (element: HTMLElement | null) => void;
  attributes?: ReturnType<typeof useSortable>["attributes"];
  onKeyDown?: React.KeyboardEventHandler;
}

export function BoardCardBody({
  card,
  now,
  silentDays,
  actions,
  handle,
  lifted = false,
}: BoardCardProps & { handle?: HandleProps; lifted?: boolean }) {
  const t = useTranslations("applications.board");
  const tStatus = useTranslations("status");
  const { age, tone } = cardAge(card, now, silentDays);
  // A closed card's line is its countdown; the source would only crowd it onto two lines.
  const group = age.kind === "leaves" ? null : sourceGroupOf(card.source);
  const showStage = card.status !== null && card.status !== "Applied" && card.status !== "Offer";
  const pill = card.unseen ? ORIGIN_PILL[card.origin] : undefined;

  return (
    <article
      className={`flex gap-1.5 rounded-lg border bg-white py-2.5 pl-1.5 pr-2.5 text-left dark:bg-gray-900 ${
        card.unseen ? "border-accent/40" : "border-gray-200 dark:border-gray-800"
      } ${lifted ? "shadow-xl ring-2 ring-accent/40" : "shadow-sm"}`}
    >
      <button
        type="button"
        ref={handle?.ref}
        {...handle?.attributes}
        onKeyDown={handle?.onKeyDown}
        aria-label={t("dragHandle", { title: `${card.jobTitle}, ${card.companyName}` })}
        className="mt-0.5 flex h-6 w-4 shrink-0 cursor-grab items-center justify-center rounded text-gray-300 hover:text-gray-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent active:cursor-grabbing dark:text-gray-600 dark:hover:text-gray-400"
      >
        <svg width="10" height="16" viewBox="0 0 10 16" fill="currentColor" aria-hidden="true">
          <circle cx="2.5" cy="3" r="1.3" />
          <circle cx="7.5" cy="3" r="1.3" />
          <circle cx="2.5" cy="8" r="1.3" />
          <circle cx="7.5" cy="8" r="1.3" />
          <circle cx="2.5" cy="13" r="1.3" />
          <circle cx="7.5" cy="13" r="1.3" />
        </svg>
      </button>

      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        {pill && (
          <span className={`inline-flex items-center gap-1.5 self-start rounded-full px-2 py-0.5 text-[11px] font-medium ${pill}`}>
            <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden="true" />
            {t(`origin.${card.origin as "Email" | "EmailReturned" | "Extension" | "Later"}`)}
          </span>
        )}
        <div className="flex items-start justify-between gap-1.5">
          <Link
            href={card.kind === "Application" ? `/applications/${card.itemId}` : "/tracked-jobs"}
            onClick={() => actions.onSeen(card)}
            className="text-sm font-semibold leading-snug text-gray-900 hover:text-accent-ink hover:underline dark:text-gray-100"
          >
            {card.jobTitle}
          </Link>
          <CardMenu
            card={card}
            onOpen={() => actions.onSeen(card)}
            onChangeStage={() => actions.onChangeStage(card)}
            onMoveToTop={() => actions.onMoveToTop(card)}
            onRemove={() => actions.onRemove(card)}
          />
        </div>
        <div className="flex min-w-0 items-center gap-2">
          <CompanyMark companyId={card.companyId} companyName={card.companyName} />
          <span className="truncate text-xs font-medium text-gray-700 dark:text-gray-300">{card.companyName}</span>
        </div>
        {showStage && card.status && (
          <span className="self-start rounded-full bg-accent-wash px-2 py-0.5 text-[11px] font-medium text-accent-ink">
            {tStatus(card.status)}
          </span>
        )}
        <div className="flex items-center gap-1.5">
          <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${TONE_DOT[tone]}`} aria-hidden="true" />
          <span className={`text-[11px] ${TONE_TEXT[tone]}`}>{t(`age.${age.kind}`, { days: age.days })}</span>
          {group && <span className="ml-auto truncate text-[11px] text-gray-500 dark:text-gray-400">{t(`source.${group}`)}</span>}
        </div>
      </div>
    </article>
  );
}
