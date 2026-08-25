import { Link } from "@/i18n/navigation";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <header className="border-b border-gray-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link href="/login" className="text-lg font-semibold text-gray-900">
            AfterApply
          </Link>
          <LanguageSwitcher />
        </div>
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </>
  );
}
