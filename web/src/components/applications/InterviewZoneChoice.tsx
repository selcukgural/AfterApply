"use client";

import { useState, useSyncExternalStore } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Select } from "@/components/ui/Select";
import {
  allZones,
  readerZone,
  timeInZone,
  zoneCity,
  zonedTimeToInstant,
  zoneQuestionNeeded,
  zonesForCountry,
} from "@/lib/applications/timeZones";
import { toInterviewInstant } from "@/lib/applications/interview";

const subscribe = () => () => {};

/** The instant the interview inputs describe: on the company's clocks when a zone was picked,
 *  on the reader's own otherwise. */
export function interviewInstant(date: string, time: string, zone: string | null): string | null {
  return zone ? zonedTimeToInstant(date, time, zone) : toInterviewInstant(date, time);
}

interface InterviewZoneChoiceProps {
  /** The company's ISO country code, when enrichment found one. */
  companyCountry: string | null | undefined;
  date: string;
  time: string;
  /** The zone the typed time is in; null means the reader's own. */
  zone: string | null;
  onZoneChange: (zone: string | null) => void;
}

/**
 * "Whose 14:00 is this?" beside the interview date and time (canvas "İnce dokunuşlar — Paket 6",
 * 5A). A time copied out of a recruiter's e-mail is usually theirs; this catches it while it is
 * being typed, with the result read back on the reader's own clock. Silent when the company keeps
 * the reader's clock; for a company of unknown country it is one small link, not a question.
 */
export function InterviewZoneChoice({ companyCountry, date, time, zone, onZoneChange }: InterviewZoneChoiceProps) {
  const t = useTranslations("applications.interview.zone");
  const locale = useLocale();
  // The reader's zone only exists in the browser; the server render asks nothing.
  const own = useSyncExternalStore(subscribe, readerZone, () => null);
  const [askingUnknown, setAskingUnknown] = useState(false);
  // Read once per mount: only which side of a daylight-saving change "no date yet" falls on.
  const [mountedAt] = useState(() => Date.now());

  if (own === null) return null;
  const companyZones = zonesForCountry(companyCountry);
  const around = new Date(/^\d{4}-\d{2}-\d{2}$/.test(date) ? `${date}T12:00:00Z` : mountedAt);
  const instant = interviewInstant(date, time, zone);
  const preview =
    zone && instant ? t("preview", { own: timeInZone(instant, own, locale), city: zoneCity(zone, locale), theirs: time }) : null;

  if (companyZones.length === 0) {
    return (
      <div className="flex basis-full flex-col gap-1.5">
        {askingUnknown || zone ? (
          <div className="w-64">
            <Select aria-label={t("pick")} value={zone ?? ""} onChange={(e) => onZoneChange(e.target.value || null)}>
              <option value="">{t("mine", { city: zoneCity(own, locale) })}</option>
              {allZones().map((z) => (
                <option key={z} value={z}>
                  {z.replace(/_/g, " ")}
                </option>
              ))}
            </Select>
          </div>
        ) : (
          <button type="button" onClick={() => setAskingUnknown(true)} className="self-start text-xs text-accent-ink hover:underline">
            {t("otherZone")}
          </button>
        )}
        {preview && <p role="status" className="text-xs text-gray-600 dark:text-gray-400">{preview}</p>}
      </div>
    );
  }

  if (!zoneQuestionNeeded(companyZones, own, around)) return null;

  const theirs = zone ?? companyZones[0];
  return (
    <fieldset className="flex basis-full flex-col gap-1.5">
      <legend className="pb-1.5 text-xs font-medium text-gray-700 dark:text-gray-300">{t("legend")}</legend>
      <div className="flex flex-wrap items-center gap-2">
        <div className="inline-flex overflow-hidden rounded-md border border-gray-300 text-sm dark:border-gray-700">
          <button
            type="button"
            aria-pressed={zone === null}
            onClick={() => onZoneChange(null)}
            className={`min-h-9 px-3 ${zone === null ? "bg-accent-wash font-medium text-accent-ink" : "bg-white text-gray-700 dark:bg-gray-900 dark:text-gray-300"}`}
          >
            {t("mine", { city: zoneCity(own, locale) })}
          </button>
          <button
            type="button"
            aria-pressed={zone !== null}
            onClick={() => onZoneChange(theirs)}
            className={`min-h-9 border-l border-gray-300 px-3 dark:border-gray-700 ${zone !== null ? "bg-accent-wash font-medium text-accent-ink" : "bg-white text-gray-700 dark:bg-gray-900 dark:text-gray-300"}`}
          >
            {t("theirs", { city: zoneCity(theirs, locale) })}
          </button>
        </div>
        {zone !== null && companyZones.length > 1 && (
          <div className="w-52">
            <Select aria-label={t("pick")} value={zone} onChange={(e) => onZoneChange(e.target.value)}>
              {companyZones.map((z) => (
                <option key={z} value={z}>
                  {zoneCity(z, locale)}
                </option>
              ))}
            </Select>
          </div>
        )}
      </div>
      {preview && <p role="status" className="text-xs text-gray-600 dark:text-gray-400">{preview}</p>}
    </fieldset>
  );
}
