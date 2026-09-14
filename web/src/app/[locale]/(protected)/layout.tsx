import { AuthGuard } from "@/components/layout/AuthGuard";
import { FeedbackWidget } from "@/components/feedback/FeedbackWidget";
import { NavBar } from "@/components/layout/NavBar";
import { NextIntlClientProvider } from "next-intl";

/**
 * The signed-in app gets the whole message catalogue: the root layout only provides the shared
 * chrome's namespaces (see messageScopes.ts), and this is the one place where "everything" is the
 * right answer — the pages behind it use most of it, and none of them is a first visit.
 */
export default async function ProtectedLayout({ children }: { children: React.ReactNode }) {
  return (
    <NextIntlClientProvider>
    <AuthGuard>
      <NavBar />
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">{children}</main>
      {/* Mounted once for the whole signed-in app rather than per page: the launcher is meant
          to be reachable from wherever the user got stuck. */}
      <FeedbackWidget />
    </AuthGuard>
    </NextIntlClientProvider>
  );
}
