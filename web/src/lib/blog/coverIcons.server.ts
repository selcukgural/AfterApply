// Server-side only: the whole icon set (≈760 KB of JSON) stays out of every browser bundle. The
// list page and the share-image route draw a cover with one icon each; the admin's picker loads
// the set on its own, and only when it opens (`coverIconSet.ts`).
import iconNodes from "lucide-static/icon-nodes.json";
import { resolveCoverIcon, type CoverIconNode } from "@/lib/blog/coverCard";

const NODES = iconNodes as unknown as Record<string, CoverIconNode>;

/** The drawing of a cover's icon: the chosen one, or the default for none or an unknown name. */
export function coverIconNode(name: string | null | undefined): CoverIconNode | null {
  return resolveCoverIcon(NODES, name);
}
