import { cookies } from "next/headers";
import type { Theme } from "./theme";

// Server-only counterpart to theme.ts's client helpers — reads the same
// cookie so layouts can stamp the right theme class/prop during SSR and
// avoid a flash of the wrong theme on first paint.
export async function getServerTheme(): Promise<Theme> {
  const store = await cookies();
  return store.get("theme")?.value === "dark" ? "dark" : "light";
}
