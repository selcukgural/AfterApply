"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { Button } from "@/components/ui/Button";

export function NavBar() {
  const { user, logout } = useAuth();
  const router = useRouter();

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  return (
    <header className="border-b border-gray-200 bg-white">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-6">
          <Link href="/" className="text-lg font-semibold text-gray-900">
            AfterApply
          </Link>
          <nav className="flex gap-4 text-sm text-gray-600">
            <Link href="/" className="hover:text-gray-900">
              Panel
            </Link>
            <Link href="/applications" className="hover:text-gray-900">
              Başvurular
            </Link>
          </nav>
        </div>
        <div className="flex items-center gap-3 text-sm text-gray-600">
          {user && (
            <span>
              {user.firstName} {user.lastName}
            </span>
          )}
          <Button variant="secondary" onClick={handleLogout}>
            Çıkış Yap
          </Button>
        </div>
      </div>
    </header>
  );
}
