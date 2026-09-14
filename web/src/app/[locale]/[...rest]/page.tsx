import { notFound } from "next/navigation";

/**
 * Catch-all under the locale segment: a URL that matches no real route lands here and is handed
 * to `[locale]/not-found.tsx`, so an unknown address gets the site's own 404 page — header, footer
 * and copy in the visitor's language — instead of Next's bare "This page could not be found." in
 * English with nowhere to go. (Route handlers such as /[locale]/og and real pages take precedence
 * over this segment; it only ever sees what nothing else claimed.)
 */
export default function CatchAllPage() {
  notFound();
}
