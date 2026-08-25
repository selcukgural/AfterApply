"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { emailIntegrationsApi } from "@/lib/api/emailIntegrations";
import { ApiError } from "@/lib/api/httpClient";
import type { EmailConnectionStatusResponse } from "@/types/api";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function SettingsPage() {
  const { deleteAccount } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();

  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const [emailStatus, setEmailStatus] = useState<EmailConnectionStatusResponse | null>(null);
  const [emailStatusLoading, setEmailStatusLoading] = useState(true);
  // Read once from the URL synchronously during the initial render (not via an effect —
  // setState-in-effect causes an avoidable extra render pass).
  const [emailActionError, setEmailActionError] = useState<string | null>(() =>
    searchParams.get("emailIntegration") === "error" ? "Gmail bağlantısı başarısız oldu. Lütfen tekrar deneyin." : null,
  );
  const [emailNotice] = useState<string | null>(() =>
    searchParams.get("emailIntegration") === "success" ? "Gmail hesabınız bağlandı." : null,
  );

  const [password, setPassword] = useState("");
  const [confirmationText, setConfirmationText] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    if (searchParams.get("emailIntegration")) {
      router.replace("/settings");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    emailIntegrationsApi
      .getStatus()
      .then(setEmailStatus)
      .catch(() => setEmailStatus(null))
      .finally(() => setEmailStatusLoading(false));
  }, []);

  const handleConnectGmail = async () => {
    setEmailActionError(null);
    try {
      const { authorizationUrl } = await emailIntegrationsApi.getAuthorizationUrl();
      window.location.href = authorizationUrl;
    } catch (error) {
      setEmailActionError(error instanceof ApiError ? error.message : "Gmail bağlantısı başlatılamadı.");
    }
  };

  const handleDisconnectGmail = async () => {
    setEmailActionError(null);
    try {
      await emailIntegrationsApi.disconnect();
      setEmailStatus((prev) => (prev ? { ...prev, connected: false, providerAccountEmail: null } : prev));
    } catch (error) {
      setEmailActionError(error instanceof ApiError ? error.message : "Bağlantı kaldırılamadı.");
    }
  };

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

      <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900">E-posta Entegrasyonu</h2>
        <p className="mb-4 text-sm text-gray-600">
          Gmail hesabınızı bağlayın; işe alım e-postalarından (mülakat daveti, ret vb.) otomatik statü
          önerileri alın. Sadece okuma izni istenir, e-posta gönderilmez.
        </p>
        {emailNotice && <p className="mb-3 text-sm text-green-700">{emailNotice}</p>}
        {emailActionError && <p className="mb-3 text-sm text-red-600">{emailActionError}</p>}
        {emailStatusLoading ? (
          <p className="text-sm text-gray-500">Yükleniyor...</p>
        ) : emailStatus?.connected ? (
          <div className="flex flex-col gap-3">
            <p className="text-sm text-gray-700">
              Bağlı hesap: <span className="font-medium">{emailStatus.providerAccountEmail}</span>
            </p>
            {emailStatus.needsReattention && (
              <p className="text-sm text-amber-600">
                Son senkronizasyon başarısız oldu, Gmail bağlantısını yeniden kurmanız gerekebilir.
              </p>
            )}
            <div className="flex items-center gap-4">
              <Link href="/settings/email-suggestions" className="text-sm text-blue-600 hover:underline">
                Bekleyen önerileri gör
              </Link>
              <Button variant="secondary" onClick={handleDisconnectGmail}>
                Bağlantıyı Kaldır
              </Button>
            </div>
          </div>
        ) : (
          <Button variant="secondary" onClick={handleConnectGmail}>
            Gmail Bağla
          </Button>
        )}
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
