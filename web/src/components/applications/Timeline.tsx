import type { ApplicationEventResponse, ApplicationStatus } from "@/types/api";
import { EVENT_TYPE_LABELS } from "@/lib/constants/eventType";
import { APPLICATION_STATUS_LABELS } from "@/lib/constants/applicationStatus";

function describeEvent(event: ApplicationEventResponse): string {
  if (event.type === "StatusChanged" && event.metadata) {
    try {
      const parsed = JSON.parse(event.metadata) as { fromStatus?: ApplicationStatus; toStatus?: ApplicationStatus };
      if (parsed.fromStatus && parsed.toStatus) {
        return `${APPLICATION_STATUS_LABELS[parsed.fromStatus]} → ${APPLICATION_STATUS_LABELS[parsed.toStatus]}`;
      }
    } catch {
      // fall through to the plain label below
    }
  }
  return EVENT_TYPE_LABELS[event.type];
}

export function Timeline({ events }: { events: ApplicationEventResponse[] }) {
  if (events.length === 0) {
    return <p className="text-sm text-gray-500">Henüz bir etkinlik yok.</p>;
  }

  return (
    <ol className="flex flex-col gap-3">
      {events.map((event) => (
        <li key={event.id} className="flex items-start gap-3 border-l-2 border-gray-200 pl-3">
          <div>
            <p className="text-sm font-medium text-gray-900">{describeEvent(event)}</p>
            <p className="text-xs text-gray-500">
              {new Date(event.occurredAt).toLocaleString("tr-TR")}
            </p>
          </div>
        </li>
      ))}
    </ol>
  );
}
