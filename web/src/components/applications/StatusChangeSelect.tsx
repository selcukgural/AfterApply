"use client";

import { useState } from "react";
import type { ApplicationStatus } from "@/types/api";
import { APPLICATION_STATUSES, APPLICATION_STATUS_LABELS } from "@/lib/constants/applicationStatus";
import { Select } from "@/components/ui/Select";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

interface StatusChangeSelectProps {
  currentStatus: ApplicationStatus;
  onChangeStatus: (newStatus: ApplicationStatus, note: string | null) => Promise<void>;
  isSubmitting: boolean;
}

export function StatusChangeSelect({ currentStatus, onChangeStatus, isSubmitting }: StatusChangeSelectProps) {
  const otherStatuses = APPLICATION_STATUSES.filter((s) => s !== currentStatus);
  const [selected, setSelected] = useState<ApplicationStatus>(otherStatuses[0]);
  const [note, setNote] = useState("");
  const [isOpen, setIsOpen] = useState(false);

  if (!isOpen) {
    return (
      <Button variant="secondary" onClick={() => setIsOpen(true)}>
        Durumu Değiştir
      </Button>
    );
  }

  const handleConfirm = async () => {
    await onChangeStatus(selected, note.trim() || null);
    setIsOpen(false);
    setNote("");
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-3">
      <Select value={selected} onChange={(e) => setSelected(e.target.value as ApplicationStatus)}>
        {otherStatuses.map((status) => (
          <option key={status} value={status}>
            {APPLICATION_STATUS_LABELS[status]}
          </option>
        ))}
      </Select>
      <Input placeholder="Not (opsiyonel)" value={note} onChange={(e) => setNote(e.target.value)} />
      <div className="flex gap-2">
        <Button onClick={handleConfirm} disabled={isSubmitting}>
          {isSubmitting ? "Kaydediliyor..." : "Onayla"}
        </Button>
        <Button variant="secondary" onClick={() => setIsOpen(false)} disabled={isSubmitting}>
          Vazgeç
        </Button>
      </div>
    </div>
  );
}
