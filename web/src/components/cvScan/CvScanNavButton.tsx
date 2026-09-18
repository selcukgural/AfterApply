"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import { cvScanPath } from "@/lib/cvScan/path";

/**
 * The scan's standing entry point, mounted in two places: the landing navbar and the public
 * layout's header. Two, because they are two different headers — /guide, /help, /privacy,
 * /benchmark and the auth pages do not use the landing navbar, and search traffic arrives on the
 * guide articles rather than on the landing page.
 *
 * Outline rather than filled: it is meant to be always available, not to compete with whichever
 * primary action the page it sits on is actually about.
 *
 * The copy lives under `cvScan` rather than `landing.navbar` because the public layout has no
 * business importing landing copy — and one string cannot drift from itself.
 */
export function CvScanNavButton({ className = "", onNavigate }: { className?: string; onNavigate?: () => void }) {
  const t = useTranslations("cvScan");
  // next-intl's usePathname returns the path without the locale prefix — /cv-tarama under /tr,
  // /cv-scan under /en — so it is compared with this locale's own slug.
  const pathname = usePathname();
  const locale = useLocale();

  // Never point at the page you are already on.
  if (pathname === cvScanPath(locale)) {
    return null;
  }

  return (
    <Link href={cvScanPath(locale)} onClick={onNavigate} className={buttonClassName("outline", className)}>
      {t("navCta")}
    </Link>
  );
}
