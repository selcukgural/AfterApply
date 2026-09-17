import { z } from "zod";

/** The name columns are 100 wide and, since 2026-09-14, optional — the same rule as sign-up.
 *  Trimmed here so a name made of spaces saves as empty, which the server treats the same way. */
export function createProfileSchema(t: (key: string) => string) {
  return z.object({
    firstName: z.string().trim().max(100, t("nameTooLong")),
    lastName: z.string().trim().max(100, t("nameTooLong")),
  });
}
