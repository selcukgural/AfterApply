"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { isExcludedVisitor, trackSiteTraffic } from "@/lib/analytics/siteTraffic";
import { useAuth } from "@/lib/auth/AuthContext";

/**
 * Reports one page view per public page, including after a client-side navigation (which fires no
 * document load, so nothing else would notice it).
 *
 * Mounted in exactly two places: the (public) layout, and the landing page. Never in
 * [locale]/layout.tsx, which also wraps the signed-in pages — that separation is the guarantee
 * that signed-in pages are never reported, and it is structural rather than a second copy of the
 * server's route allowlist that would drift away from it. The API drops anything unrecognised
 * regardless.
 *
 * The landing page needs its own mount because it is not in the (public) group. Until 2026-09-10
 * it had none, so "/" sat in the server's allowlist with no caller and the funnel's first step was
 * never counted.
 *
 * usePathname() from next/navigation, not from @/i18n/navigation: the counter wants the real URL
 * including its /tr or /en prefix, because which language a visitor arrives in is one of the
 * things being measured. The i18n wrapper strips exactly that prefix.
 */
export function SiteTrafficReporter() {
  const pathname = usePathname();
  const { user, isLoading } = useAuth();
  // Decided here and not sent anywhere: an admin's own visits are not what the counter is for.
  // Waits for the session to load first — reporting on the first render and excluding on the
  // second would count the admin's arrival anyway.
  const excluded = isExcludedVisitor(user);

  useEffect(() => {
    if (isLoading || excluded) return;
    trackSiteTraffic("page_view", pathname);
  }, [pathname, isLoading, excluded]);

  return null;
}
