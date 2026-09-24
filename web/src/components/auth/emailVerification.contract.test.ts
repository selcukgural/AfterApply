import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Source scan, like the other contract tests — there is no render harness. Email verification at
 * sign-up (2026-09-24): every page that can receive a pending verification shows the code step in
 * place instead of treating the answer as a session, and the ticket never touches browser storage.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the email verification step", () => {
  it("is shown by register, login and all three provider callbacks", () => {
    for (const file of [
      "app/[locale]/(public)/register/page.tsx",
      "app/[locale]/(public)/login/page.tsx",
      "app/[locale]/(public)/auth/google/callback/page.tsx",
      "app/[locale]/(public)/auth/linkedin/callback/page.tsx",
      "app/[locale]/(public)/auth/github/callback/page.tsx",
    ]) {
      expect(read(file), file).toContain("<EmailVerificationStep pending=");
    }
  });

  it("is reached from every provider callback's sign-in and sign-up answers", () => {
    for (const provider of ["google", "linkedin", "github"]) {
      const page = read(`app/[locale]/(public)/auth/${provider}/callback/page.tsx`);
      expect(page).toContain("result.pendingVerification");
      expect(page).toContain("if (isVerificationPending(outcome))");
    }
  });

  it("stores a session only once there is one", () => {
    const context = read("lib/auth/AuthContext.tsx");
    expect(context).toContain("if (!isVerificationPending(outcome))");
    // Registration always answers with a pending verification, so it never stores anything.
    expect(context).toContain("const register = useCallback((request: RegisterRequest) => authApi.register(request), []);");
  });

  it("keeps the ticket out of browser storage", () => {
    const step = read("components/auth/EmailVerificationStep.tsx");
    expect(step).not.toMatch(/(local|session)Storage/);
  });

  it("never sends a bearer token with the ticket, nor mistakes its 400 for an expired session", () => {
    expect(read("lib/api/httpClient.ts")).toContain('"/api/auth/verify-email",');
  });
});
