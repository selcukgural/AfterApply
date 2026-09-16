"use client";

import { useTranslations } from "next-intl";
import { NavMenu, type NavMenuItem } from "@/components/layout/NavMenu";
import { ProBadge } from "@/components/layout/ProBadge";
import { useProBadge } from "@/hooks/useProBadge";

/** The things that work without an account, kept reachable after you have one. */
export const TOOL_LINKS = [
  { href: "/cv-tarama", key: "cvScan" },
  { href: "/benchmark", key: "benchmark" },
  { href: "/guide", key: "guide" },
] as const;

/**
 * "Keşfet ▾": what is out there to look at, as opposed to the buyer's own data — the weekly
 * postings (paid), the company pages, and the account-free tools that used to be the "Tools"
 * menu. One flat list on purpose: a first draft split it into "needs an account / does not",
 * which read as "Companies needs Pro" — the Pro badge on the one paid item says all there is
 * to say about who gets what.
 *
 * The weekly-postings item follows the server flag (a dark deployment has no such route), and
 * carries the Pro badge only for an account that is not Pro yet: it is the promise of a gate
 * page, not a lock, so the item stays visible to everyone.
 */
export function ExploreMenu() {
  const t = useTranslations("nav");
  const { weeklyJobsEnabled, showProBadge } = useProBadge();

  const items: NavMenuItem[] = [
    ...(weeklyJobsEnabled
      ? [
          {
            href: "/weekly-jobs",
            label: (
              <>
                {t("weeklyJobs")}
                {showProBadge && <ProBadge />}
              </>
            ),
          },
        ]
      : []),
    // Public page, but listed here too: a signed-in person is the one who can write a review.
    { href: "/companies", label: t("companies") },
    ...TOOL_LINKS.map((link) => ({ href: link.href, label: t(link.key) })),
  ];

  return <NavMenu label={t("explore")} items={items} />;
}
