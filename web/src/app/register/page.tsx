"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { registerSchema } from "@/lib/validation/registerSchema";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

type FieldErrors = Partial<Record<"email" | "password" | "firstName" | "lastName", string>>;

export default function RegisterPage() {
  const { register } = useAuth();
  const router = useRouter();
  const [values, setValues] = useState({ email: "", password: "", firstName: "", lastName: "" });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = (field: keyof typeof values) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = registerSchema.safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({
        email: fieldErrors.email?.[0],
        password: fieldErrors.password?.[0],
        firstName: fieldErrors.firstName?.[0],
        lastName: fieldErrors.lastName?.[0],
      });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      await register(result.data);
      router.push("/");
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Kayıt oluşturulamadı.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="mb-6 text-xl font-semibold text-gray-900">Kayıt Ol</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Ad" htmlFor="firstName" error={errors.firstName}>
              <Input id="firstName" value={values.firstName} onChange={update("firstName")} />
            </FormField>
            <FormField label="Soyad" htmlFor="lastName" error={errors.lastName}>
              <Input id="lastName" value={values.lastName} onChange={update("lastName")} />
            </FormField>
          </div>
          <FormField label="E-posta" htmlFor="email" error={errors.email}>
            <Input id="email" type="email" value={values.email} onChange={update("email")} autoComplete="email" />
          </FormField>
          <FormField label="Şifre" htmlFor="password" error={errors.password}>
            <Input
              id="password"
              type="password"
              value={values.password}
              onChange={update("password")}
              autoComplete="new-password"
            />
          </FormField>
          {formError && <p className="text-sm text-red-600">{formError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Kayıt oluşturuluyor..." : "Kayıt Ol"}
          </Button>
        </form>
        <p className="mt-4 text-sm text-gray-600">
          Zaten hesabın var mı?{" "}
          <Link href="/login" className="text-blue-600 hover:underline">
            Giriş yap
          </Link>
        </p>
      </div>
    </div>
  );
}
