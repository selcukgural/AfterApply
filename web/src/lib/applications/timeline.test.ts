import { describe, expect, it } from "vitest";
import type { ApplicationEventResponse, ApplicationStatusHistoryResponse } from "@/types/api";
import { mergeTimeline, readEventNote, writeEventNote } from "./timeline";

function status(id: string, changedAt: string): ApplicationStatusHistoryResponse {
  return {
    id,
    fromStatus: "Applied",
    toStatus: "Interview",
    changedAt,
    origin: "Manual",
    note: null,
    emailSubject: null,
    emailSnippet: null,
    rejectionReasonCategory: null,
    rejectionReasonDetail: null,
  } as ApplicationStatusHistoryResponse;
}

function event(
  id: string,
  occurredAt: string,
  type: ApplicationEventResponse["type"] = "FollowUpSent",
): ApplicationEventResponse {
  return { id, type, occurredAt, source: "Manual", metadata: null };
}

describe("mergeTimeline", () => {
  it("interleaves both sources newest first", () => {
    const merged = mergeTimeline(
      [status("s1", "2026-09-05T10:00:00Z"), status("s2", "2026-09-01T10:00:00Z")],
      [event("e1", "2026-09-03T10:00:00Z")],
    );

    expect(merged.map((item) => item.id)).toEqual(["s1", "e1", "s2"]);
  });

  it("returns an empty list when there is nothing at all", () => {
    expect(mergeTimeline([], [])).toEqual([]);
  });

  it("passes a single-sided list through untouched", () => {
    expect(mergeTimeline([], [event("e1", "2026-09-03T10:00:00Z")]).map((i) => i.kind)).toEqual(["event"]);
    expect(mergeTimeline([status("s1", "2026-09-03T10:00:00Z")], []).map((i) => i.kind)).toEqual(["status"]);
  });

  it("keeps the server's own ordering for rows sharing a timestamp", () => {
    // GetStatusHistoryAsync deliberately sorts the seed row last among equals; a concat-then-sort
    // would be free to reshuffle these, a merge of pre-sorted lists cannot.
    const merged = mergeTimeline(
      [status("newer", "2026-09-05T10:00:00Z"), status("seed", "2026-09-05T10:00:00Z")],
      [],
    );

    expect(merged.map((item) => item.id)).toEqual(["newer", "seed"]);
  });

  it("puts the status change before an event recorded in the same second", () => {
    const merged = mergeTimeline([status("s1", "2026-09-05T10:00:00Z")], [event("e1", "2026-09-05T10:00:00Z")]);

    expect(merged.map((item) => item.id)).toEqual(["s1", "e1"]);
  });

  it("drops the events the status history already tells", () => {
    // Every status change writes a StatusChanged event as well as a history row, and creating an
    // application writes ApplicationCreated. Without this filter the list shows each one twice.
    const merged = mergeTimeline(
      [status("s1", "2026-09-05T10:00:00Z")],
      [
        event("e-status", "2026-09-05T10:00:00Z", "StatusChanged"),
        event("e-created", "2026-09-01T10:00:00Z", "ApplicationCreated"),
        event("e-submitted", "2026-09-01T09:00:00Z", "ApplicationSubmitted"),
      ],
    );

    expect(merged.map((item) => item.id)).toEqual(["s1"]);
  });

  it("keeps events the status history says nothing about", () => {
    const merged = mergeTimeline(
      [],
      [event("e1", "2026-09-05T10:00:00Z", "InterviewScheduled"), event("e2", "2026-09-04T10:00:00Z")],
    );

    expect(merged.map((item) => item.id)).toEqual(["e1", "e2"]);
  });
});

describe("event notes travel through the jsonb Metadata column", () => {
  it("wraps a typed note so Postgres accepts it", () => {
    expect(writeEventNote("Zoom üzerinden ayarlandı")).toBe('{"note":"Zoom üzerinden ayarlandı"}');
  });

  it("sends null rather than empty JSON for a blank note", () => {
    expect(writeEventNote(null)).toBeNull();
    expect(writeEventNote("   ")).toBeNull();
  });

  it("reads back what it wrote", () => {
    expect(readEventNote(writeEventNote("Takip maili attım"))).toBe("Takip maili attım");
  });

  it("shows nothing for a status change's own metadata", () => {
    // Application.ChangeStatus writes this shape into the same column.
    expect(readEventNote('{"fromStatus":"Applied","toStatus":"Interview"}')).toBeNull();
  });

  it("never throws on metadata it does not recognise", () => {
    expect(readEventNote(null)).toBeNull();
    expect(readEventNote("not json at all")).toBeNull();
    expect(readEventNote('{"note":42}')).toBeNull();
    expect(readEventNote("[1,2,3]")).toBeNull();
  });
});
