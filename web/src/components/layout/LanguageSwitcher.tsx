"use client";

import { useLocale } from "next-intl";
import { usePathname, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";

export function LanguageSwitcher() {
  const locale = useLocale();
  const pathname = usePathname();
  const router = useRouter();

  return (
    <div className="flex items-center gap-1 text-sm text-gray-600">
      {routing.locales.map((code) => (
        <button
          key={code}
          type="button"
          onClick={() => router.replace(pathname, { locale: code })}
          disabled={code === locale}
          className={
            code === locale
              ? "font-semibold text-gray-900"
              : "text-gray-500 hover:text-gray-900"
          }
        >
          {code.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
