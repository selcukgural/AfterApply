"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationEventType } from "@/types/api";
import { MANUAL_APPLICATION_EVENT_TYPES } from "@/lib/constants/applicationEventType";
import { Select } from "@/components/ui/Select";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

interface AddEventFormProps {
  onAddEvent: (type: ApplicationEventType, occurredAt: string | null, note: string | null) => Promise<void>;
  isSubmitting: boolean;
}

/**
 * Records something that happened outside a status change — a follow-up sent, an interview booked.
 * Collapsed until asked for, the same way StatusChangeSelect is: the detail page is for reading
 * first, and a form permanently open at the bottom of it reads as work waiting to be done.
 */
export function AddEventForm({ onAddEvent, isSubmitting }: AddEventFormProps) {
  const t = useTranslations("applications.addEvent");
  const tEventType = useTranslations("applicationEventType");
  const [isOpen, setIsOpen] = useState(false);
  const [type, setType] = useState<ApplicationEventType>(MANUAL_APPLICATION_EVENT_TYPES[0]);
  const [occurredAt, setOccurredAt] = useState("");
  const [note, setNote] = useState("");

  if (!isOpen) {
    return (
      <Button variant="secondary" onClick={() => setIsOpen(true)}>
        {t("addEvent")}
      </Button>
    );
  }

  const close = () => {
    setIsOpen(false);
    setOccurredAt("");
    setNote("");
  };

  const handleConfirm = async () => {
    // A datetime-local value has no zone; it is what the user sees on their own clock, so read it
    // as local time and send an instant. Empty means "now" — the server stamps it.
    const at = occurredAt ? new Date(occurredAt).toISOString() : null;
    await onAddEvent(type, at, note.trim() || null);
    close();
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-800">
      <Select value={type} onChange={(e) => setType(e.target.value as ApplicationEventType)} aria-label={t("type")}>
        {MANUAL_APPLICATION_EVENT_TYPES.map((eventType) => (
          <option key={eventType} value={eventType}>
            {tEventType(eventType)}
          </option>
        ))}
      </Select>
      <Input
        type="datetime-local"
        value={occurredAt}
        onChange={(e) => setOccurredAt(e.target.value)}
        aria-label={t("occurredAt")}
      />
      <Input placeholder={t("notePlaceholder")} value={note} onChange={(e) => setNote(e.target.value)} />
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("occurredAtHint")}</p>
      <div className="flex gap-2">
        <Button onClick={handleConfirm} disabled={isSubmitting}>
          {isSubmitting ? t("saving") : t("confirm")}
        </Button>
        <Button variant="secondary" onClick={close} disabled={isSubmitting}>
          {t("cancel")}
        </Button>
      </div>
    </div>
  );
}
