"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { emailIntegrationsApi } from "@/lib/api/emailIntegrations";
import { ApiError } from "@/lib/api/httpClient";
import type { EmailSuggestionResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { Button } from "@/components/ui/Button";

export default function EmailSuggestionsPage() {
  const [suggestions, setSuggestions] = useState<EmailSuggestionResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingActionId, setPendingActionId] = useState<string | null>(null);

  useEffect(() => {
    emailIntegrationsApi
      .getPendingSuggestions()
      .then(setSuggestions)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Öneriler yüklenemedi."));
  }, []);

  const handleConfirm = async (id: string) => {
    setPendingActionId(id);
    setError(null);
    try {
      await emailIntegrationsApi.confirmSuggestion(id);
      setSuggestions((prev) => prev?.filter((s) => s.id !== id) ?? prev);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Onaylanamadı.");
    } finally {
      setPendingActionId(null);
    }
  };

  const handleDismiss = async (id: string) => {
    setPendingActionId(id);
    setError(null);
    try {
      await emailIntegrationsApi.dismissSuggestion(id);
      setSuggestions((prev) => prev?.filter((s) => s.id !== id) ?? prev);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Reddedilemedi.");
    } finally {
      setPendingActionId(null);
    }
  };

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div>
        <Link href="/settings" className="text-sm text-blue-600 hover:underline">
          ← Hesap Ayarları
        </Link>
        <h1 className="mt-2 text-xl font-semibold text-gray-900">Gmail Önerileri</h1>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      {suggestions === null ? (
        <p className="text-sm text-gray-500">Yükleniyor...</p>
      ) : suggestions.length === 0 ? (
        <p className="text-sm text-gray-500">Bekleyen öneri yok.</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {suggestions.map((s) => (
            <li key={s.id} className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
              <div className="mb-2 flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium text-gray-900">
                    {s.companyName} — {s.jobTitle}
                  </p>
                  <p className="text-xs text-gray-500">
                    {new Date(s.emailReceivedAt).toLocaleString("tr-TR")} · Güven: {Math.round(s.confidenceScore * 100)}%
                  </p>
                </div>
                {s.suggestedStatus ? (
                  <StatusBadge status={s.suggestedStatus} />
                ) : (
                  <span className="inline-block rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600">
                    Hâlâ bekleniyor
                  </span>
                )}
              </div>
              <p className="mb-1 text-sm font-medium text-gray-800">{s.subject}</p>
              <p className="mb-3 text-sm text-gray-600">{s.snippet}</p>
              <div className="flex gap-3">
                {s.suggestedStatus && (
                  <Button onClick={() => handleConfirm(s.id)} disabled={pendingActionId === s.id}>
                    Onayla
                  </Button>
                )}
                <Button
                  variant="secondary"
                  onClick={() => handleDismiss(s.id)}
                  disabled={pendingActionId === s.id}
                >
                  Yoksay
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
