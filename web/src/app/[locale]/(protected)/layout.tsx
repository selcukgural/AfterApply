import { AuthGuard } from "@/components/layout/AuthGuard";
import { NavBar } from "@/components/layout/NavBar";
import { getServerTheme } from "@/lib/theme/getServerTheme";

export default async function ProtectedLayout({ children }: { children: React.ReactNode }) {
  const theme = await getServerTheme();

  return (
    <AuthGuard>
      <NavBar initialTheme={theme} />
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">{children}</main>
    </AuthGuard>
  );
}
