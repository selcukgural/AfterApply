import { AuthGuard } from "@/components/layout/AuthGuard";
import { NavBar } from "@/components/layout/NavBar";

export default function ProtectedLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard>
      <NavBar />
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">{children}</main>
    </AuthGuard>
  );
}
