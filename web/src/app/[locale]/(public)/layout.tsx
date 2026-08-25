import { Link } from "@/i18n/navigation";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { getServerTheme } from "@/lib/theme/getServerTheme";

export default async function PublicLayout({ children }: { children: React.ReactNode }) {
  const theme = await getServerTheme();

  return (
    <>
      <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link href="/login" className="text-lg font-semibold text-gray-900 dark:text-gray-100">
            AfterApply
          </Link>
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeSwitcher initialTheme={theme} />
          </div>
        </div>
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </>
  );
}
