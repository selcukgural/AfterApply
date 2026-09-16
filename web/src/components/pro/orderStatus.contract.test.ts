import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import type { PaymentOrderStatus } from "@/types/api";
import { statusKey } from "./OrderStatusBadge";

// Every order status the API can return, in the casing the API uses. A status added to the enum
// without a label here renders as "payments.status.x" on the badge and in the admin filter.
const STATUSES: PaymentOrderStatus[] = [
  "Pending",
  "Paid",
  "Failed",
  "Expired",
  "Cancelled",
  "RefundRequested",
  "Refunded",
  "PartiallyRefunded",
];

describe("order status labels", () => {
  it.each(["tr", "en"] as const)("exist for every status in %s", (locale) => {
    const labels = (locale === "tr" ? tr : en).payments.status as Record<string, string>;
    const missing = STATUSES.map(statusKey).filter((key) => !labels[key]);
    expect(missing).toEqual([]);
  });

  // 2026-09-16: the admin page's status filter asked for adminPayments.status.* — a namespace that
  // never existed — so every <option> rendered its own key. The labels are the badge's; the
  // filter must read them from the same place, through the same key function.
  it("are read from payments.status by the admin filter, not from a namespace of its own", () => {
    const page = readFileSync(
      path.join(process.cwd(), "src/app/[locale]/(protected)/admin/payments/page.tsx"),
      "utf8",
    );
    expect(page).toContain('useTranslations("payments.status")');
    expect(page).toContain("tStatus(statusKey(status))");
    expect(page).not.toMatch(/t\(`status\./);
  });
});
