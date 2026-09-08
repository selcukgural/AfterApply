import { serializeJsonLd, type JsonLdNode } from "@/lib/seo/jsonLd";

/**
 * A `<script type="application/ld+json">` element.
 *
 * `dangerouslySetInnerHTML` is the only way React will write a raw script body, and a JSON-LD block
 * has to be exactly that — React would otherwise HTML-escape the quotes and the JSON would not
 * parse. The payload is our own copy, run through `serializeJsonLd`, which escapes the characters
 * that could close the element early.
 */
export function JsonLd({ data }: { data: JsonLdNode }) {
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: serializeJsonLd(data) }} />;
}
