import type { ApplicationEventResponse, ApplicationEventType, ApplicationStatusHistoryResponse } from "@/types/api";

/**
 * One row of the application's timeline. Status changes and added events are two separate API
 * resources but a single story to the person reading them, so the detail view merges them rather
 * than stacking two competing lists.
 */
export type TimelineItem =
  | { kind: "status"; at: string; id: string; entry: ApplicationStatusHistoryResponse }
  | { kind: "event"; at: string; id: string; event: ApplicationEventResponse };

/**
 * Event types the status history already tells better, so the merged view drops them.
 *
 * Every status change writes *both* a status-history row and a StatusChanged event
 * (Application.ChangeStatus), and creating an application writes an ApplicationCreated event
 * alongside its seed history row (Application.Create). Merging the two lists naively shows each of
 * those twice. The status row is the better of the pair — it carries from → to badges, the origin
 * chip, the note and the originating email — so the event half is the one that goes.
 */
const DERIVED_EVENT_TYPES: ReadonlySet<ApplicationEventType> = new Set<ApplicationEventType>([
  "ApplicationCreated",
  "ApplicationSubmitted",
  "StatusChanged",
]);

export function isDerivedEvent(event: ApplicationEventResponse): boolean {
  return DERIVED_EVENT_TYPES.has(event.type);
}

/**
 * ApplicationEvent.Metadata is a jsonb column, not free text — Application.ChangeStatus writes
 * `{"fromStatus":...,"toStatus":...}` into it, and Postgres rejects anything that is not valid
 * JSON. A note typed by a user therefore travels as `{"note":"..."}`; these two functions are the
 * only places that shape is written and read.
 */
export function writeEventNote(note: string | null): string | null {
  const trimmed = note?.trim();
  return trimmed ? JSON.stringify({ note: trimmed }) : null;
}

/**
 * Returns the note a user typed, or null for metadata that carries something else (a status
 * change's from/to pair) or nothing at all. Never throws: this reads a column other writers also
 * write, so unexpected shapes are a normal outcome, not an error.
 */
export function readEventNote(metadata: string | null): string | null {
  if (!metadata) {
    return null;
  }

  try {
    const parsed: unknown = JSON.parse(metadata);
    if (parsed !== null && typeof parsed === "object" && "note" in parsed) {
      const note = (parsed as { note: unknown }).note;
      return typeof note === "string" && note.length > 0 ? note : null;
    }
  } catch {
    // Not JSON at all. Nothing to show — and nothing worth surfacing to the reader either.
  }

  return null;
}

/**
 * Merges the two newest-first lists into one newest-first list, dropping events the status history
 * already covers (see DERIVED_EVENT_TYPES).
 *
 * A merge of pre-sorted inputs, not a concat-then-sort, and deliberately so: the status history
 * arrives with a tie-break the server had to think about (rows sharing a ChangedAt, where the seed
 * row must sort last among equals — see GetStatusHistoryAsync), and re-sorting the combined array
 * would throw that away. Stepping through both lists preserves each one's own order exactly.
 *
 * On an exact timestamp tie *between* the two lists the status change goes first: it is the thing
 * that actually moved, and a note added in the same second reads as commentary on it.
 */
export function mergeTimeline(
  history: readonly ApplicationStatusHistoryResponse[],
  events: readonly ApplicationEventResponse[],
): TimelineItem[] {
  const ownEvents = events.filter((event) => !isDerivedEvent(event));
  const merged: TimelineItem[] = [];
  let h = 0;
  let e = 0;

  while (h < history.length || e < ownEvents.length) {
    const nextStatus = history[h];
    const nextEvent = ownEvents[e];

    const takeStatus =
      nextEvent === undefined ||
      (nextStatus !== undefined &&
        new Date(nextStatus.changedAt).getTime() >= new Date(nextEvent.occurredAt).getTime());

    if (takeStatus && nextStatus !== undefined) {
      merged.push({ kind: "status", at: nextStatus.changedAt, id: nextStatus.id, entry: nextStatus });
      h += 1;
    } else if (nextEvent !== undefined) {
      merged.push({ kind: "event", at: nextEvent.occurredAt, id: nextEvent.id, event: nextEvent });
      e += 1;
    }
  }

  return merged;
}
