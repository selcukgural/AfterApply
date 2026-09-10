import { Link } from "@/i18n/navigation";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { getServerTheme } from "@/lib/theme/getServerTheme";
import { SiteTrafficReporter } from "@/components/analytics/SiteTrafficReporter";
import { CvScanNavButton } from "@/components/cvScan/CvScanNavButton";

export default async function PublicLayout({ children }: { children: React.ReactNode }) {
  const theme = await getServerTheme();

  return (
    <>
      <SiteTrafficReporter />
      <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link href="/">
            <Logo />
          </Link>
          <div className="flex items-center gap-3">
            {/* These pages do not use the landing navbar, and search traffic arrives on the guide
                articles rather than on the landing page — so without this the one thing a stranger
                can use without an account is invisible exactly where strangers turn up. It hides
                itself on /cv-tarama. */}
            <CvScanNavButton />
            <LanguageSwitcher />
            <ThemeSwitcher initialTheme={theme} />
          </div>
        </div>
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </>
  );
}
