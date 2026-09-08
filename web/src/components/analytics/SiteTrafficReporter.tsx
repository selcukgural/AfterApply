"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";

/**
 * Reports one page view per public page, including after a client-side navigation (which fires no
 * document load, so nothing else would notice it).
 *
 * Mounted in the (public) layout and nowhere else. That placement is the guarantee that signed-in
 * pages are never reported — it is structural, rather than a second copy of the server's route
 * allowlist that would drift away from it. The API drops anything unrecognised regardless.
 *
 * usePathname() from next/navigation, not from @/i18n/navigation: the counter wants the real URL
 * including its /tr or /en prefix, because which language a visitor arrives in is one of the
 * things being measured. The i18n wrapper strips exactly that prefix.
 */
export function SiteTrafficReporter() {
  const pathname = usePathname();

  useEffect(() => {
    trackSiteTraffic("page_view", pathname);
  }, [pathname]);

  return null;
}
