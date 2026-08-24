"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { loginSchema } from "@/lib/validation/loginSchema";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = loginSchema.safeParse({ email, password });
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({ email: fieldErrors.email?.[0], password: fieldErrors.password?.[0] });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      await login(result.data);
      router.push("/");
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Giriş yapılamadı.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="mb-6 text-xl font-semibold text-gray-900">Giriş Yap</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <FormField label="E-posta" htmlFor="email" error={errors.email}>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
            />
          </FormField>
          <FormField label="Şifre" htmlFor="password" error={errors.password}>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
          </FormField>
          {formError && <p className="text-sm text-red-600">{formError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Giriş yapılıyor..." : "Giriş Yap"}
          </Button>
        </form>
        <p className="mt-4 text-sm text-gray-600">
          Hesabın yok mu?{" "}
          <Link href="/register" className="text-blue-600 hover:underline">
            Kayıt ol
          </Link>
        </p>
      </div>
    </div>
  );
}
