/**
 * What the header calls the signed-in person. A name when there is one; otherwise the local part
 * of the e-mail address, which is what they typed to get in and the only thing we are sure they
 * recognise. Sign-up stopped asking for a name on 2026-09-14, so the fallback is the common case
 * for new accounts until they fill it in under Settings.
 */
export function displayName(
  user: { firstName?: string | null; lastName?: string | null; email?: string | null } | null | undefined,
): { name: string; initials: string } {
  if (!user) return { name: "", initials: "" };

  const first = user.firstName?.trim() ?? "";
  const last = user.lastName?.trim() ?? "";
  if (first || last) {
    return {
      name: [first, last].filter(Boolean).join(" "),
      initials: `${first[0] ?? ""}${last[0] ?? ""}`.toUpperCase(),
    };
  }

  const local = (user.email ?? "").split("@")[0].trim();
  return { name: local, initials: local.slice(0, 1).toUpperCase() };
}
