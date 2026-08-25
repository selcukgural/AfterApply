"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function SettingsPage() {
  const { deleteAccount } = useAuth();
  const router = useRouter();

  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const [password, setPassword] = useState("");
  const [confirmationText, setConfirmationText] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const handleExport = async () => {
    setExportError(null);
    setIsExporting(true);
    try {
      await authApi.exportData();
    } catch (error) {
      setExportError(error instanceof ApiError ? error.message : "Veriler dışa aktarılamadı.");
    } finally {
      setIsExporting(false);
    }
  };

  const handleDelete = async (event: FormEvent) => {
    event.preventDefault();
    setDeleteError(null);

    if (confirmationText !== "SİL") {
      setDeleteError('Onaylamak için kutuya "SİL" yazın.');
      return;
    }

    setIsDeleting(true);
    try {
      await deleteAccount(password);
      router.replace("/login");
    } catch (error) {
      setDeleteError(error instanceof ApiError ? error.message : "Hesap silinemedi.");
      setIsDeleting(false);
    }
  };

  return (
    <div className="flex max-w-lg flex-col gap-8">
      <h1 className="text-xl font-semibold text-gray-900">Hesap Ayarları</h1>

      <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900">Verilerimi Dışa Aktar</h2>
        <p className="mb-4 text-sm text-gray-600">
          Hesabınıza ait tüm başvuru, import ve hatırlatma verilerini JSON dosyası olarak indirin.
        </p>
        {exportError && <p className="mb-3 text-sm text-red-600">{exportError}</p>}
        <Button variant="secondary" onClick={handleExport} disabled={isExporting}>
          {isExporting ? "Hazırlanıyor..." : "Verilerimi İndir"}
        </Button>
      </section>

      <section className="rounded-lg border border-red-200 bg-white p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900">Hesabımı Sil</h2>
        <p className="mb-4 text-sm text-gray-600">
          Bu işlem geri alınamaz. Hesabınız ve tüm başvuru, import ve hatırlatma verileriniz kalıcı olarak
          silinir.
        </p>
        <form onSubmit={handleDelete} className="flex flex-col gap-4">
          <FormField label="Şifreniz" htmlFor="delete-password">
            <Input
              id="delete-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </FormField>
          <FormField label='Onaylamak için "SİL" yazın' htmlFor="delete-confirmation">
            <Input
              id="delete-confirmation"
              value={confirmationText}
              onChange={(e) => setConfirmationText(e.target.value)}
            />
          </FormField>
          {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}
          <Button type="submit" variant="danger" disabled={isDeleting}>
            {isDeleting ? "Siliniyor..." : "Hesabımı Kalıcı Olarak Sil"}
          </Button>
        </form>
      </section>
    </div>
  );
}
