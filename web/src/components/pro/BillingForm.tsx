"use client";

import { useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Textarea } from "@/components/ui/Textarea";

export interface BillingDetails {
  billingName: string;
  billingAddress: string;
  billingPhone: string;
}

interface BillingFormProps {
  initialName: string;
  busy: boolean;
  error: string | null;
  onSubmit: (details: BillingDetails) => void;
}

/**
 * Step 1 of the checkout: what PayTR requires (name, address, phone — none of it goes into the
 * signed token, all of it is required non-empty) and what an invoice needs. The consent box is
 * the distance-sales agreement plus the explicit "perform the digital service at once" approval
 * that waives the withdrawal right — the wording of that sentence is a legal requirement, not
 * copy.
 */
export function BillingForm({ initialName, busy, error, onSubmit }: BillingFormProps) {
  const t = useTranslations("payments.checkout");
  const [name, setName] = useState(initialName);
  const [address, setAddress] = useState("");
  const [phone, setPhone] = useState("");
  const [accepted, setAccepted] = useState(false);
  const [termsError, setTermsError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!accepted) {
      setTermsError(t("termsRequired"));
      return;
    }
    setTermsError(null);
    onSubmit({ billingName: name.trim(), billingAddress: address.trim(), billingPhone: phone.trim() });
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
      <FormField label={t("billingName")} htmlFor="billing-name">
        <Input id="billing-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={60} required autoComplete="name" />
      </FormField>
      <FormField label={t("billingAddress")} htmlFor="billing-address">
        <Textarea
          id="billing-address"
          value={address}
          onChange={(e) => setAddress(e.target.value)}
          maxLength={400}
          rows={3}
          required
          autoComplete="street-address"
        />
      </FormField>
      <FormField label={t("billingPhone")} htmlFor="billing-phone">
        <Input
          id="billing-phone"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          maxLength={20}
          required
          inputMode="tel"
          autoComplete="tel"
          placeholder="+90 5xx xxx xx xx"
        />
      </FormField>
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("billingWhy")}</p>
      <Checkbox
        id="accept-terms"
        checked={accepted}
        onChange={(e) => setAccepted(e.target.checked)}
        error={termsError ?? undefined}
        label={t.rich("terms", {
          sale: (chunks) => (
            <Link href="/terms-of-sale" target="_blank" className="underline">
              {chunks}
            </Link>
          ),
          refund: (chunks) => (
            <Link href="/refund-policy" target="_blank" className="underline">
              {chunks}
            </Link>
          ),
        })}
      />
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={busy}>
          {busy ? t("starting") : t("pay")}
        </Button>
        <Link href="/pro" className="text-sm text-gray-600 underline dark:text-gray-400">
          {t("back")}
        </Link>
      </div>
    </form>
  );
}
