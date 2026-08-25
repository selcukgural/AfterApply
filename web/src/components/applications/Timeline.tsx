"use client";

import { useLocale, useTranslations } from "next-intl";
import type { ApplicationEventResponse, ApplicationStatus } from "@/types/api";

export function Timeline({ events }: { events: ApplicationEventResponse[] }) {
  const t = useTranslations("applications.timeline");
  const tStatus = useTranslations("status");
  const tEvent = useTranslations("eventType");
  const locale = useLocale();

  function describeEvent(event: ApplicationEventResponse): string {
    if (event.type === "StatusChanged" && event.metadata) {
      try {
        const parsed = JSON.parse(event.metadata) as { fromStatus?: ApplicationStatus; toStatus?: ApplicationStatus };
        if (parsed.fromStatus && parsed.toStatus) {
          return `${tStatus(parsed.fromStatus)} → ${tStatus(parsed.toStatus)}`;
        }
      } catch {
        // fall through to the plain label below
      }
    }
    return tEvent(event.type);
  }

  if (events.length === 0) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>;
  }

  return (
    <ol className="flex flex-col gap-3">
      {events.map((event) => (
        <li key={event.id} className="flex items-start gap-3 border-l-2 border-gray-200 dark:border-gray-800 pl-3">
          <div>
            <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{describeEvent(event)}</p>
            <p className="text-xs text-gray-500 dark:text-gray-400">
              {new Date(event.occurredAt).toLocaleString(locale)}
            </p>
          </div>
        </li>
      ))}
    </ol>
  );
}
