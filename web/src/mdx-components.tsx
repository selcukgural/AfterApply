import type { MDXComponents } from "mdx/types";
import { Link } from "@/i18n/navigation";

/**
 * How the guide articles' markdown renders.
 *
 * Required by `@next/mdx` under the App Router — without this file the loader has no component map
 * and the build fails. Everything here is prose styling for `src/content/guide/*.mdx`; the articles
 * themselves contain no JSX, so this is the only place their look is decided.
 */
const components: MDXComponents = {
  h2: ({ children }) => (
    <h2 className="mt-12 scroll-mt-24 text-2xl font-semibold text-gray-900 first:mt-0 dark:text-gray-100">
      {children}
    </h2>
  ),
  h3: ({ children }) => (
    <h3 className="mt-8 text-lg font-semibold text-gray-900 dark:text-gray-100">{children}</h3>
  ),
  p: ({ children }) => <p className="mt-4 leading-7 text-gray-700 dark:text-gray-300">{children}</p>,
  ul: ({ children }) => (
    <ul className="mt-4 flex list-disc flex-col gap-2 pl-5 leading-7 text-gray-700 dark:text-gray-300">{children}</ul>
  ),
  ol: ({ children }) => (
    <ol className="mt-4 flex list-decimal flex-col gap-2 pl-5 leading-7 text-gray-700 dark:text-gray-300">{children}</ol>
  ),
  li: ({ children }) => <li className="pl-1">{children}</li>,
  strong: ({ children }) => <strong className="font-semibold text-gray-900 dark:text-gray-100">{children}</strong>,
  blockquote: ({ children }) => (
    <blockquote className="mt-6 border-l-4 border-blue-200 bg-blue-50/50 py-1 pl-4 text-gray-700 dark:border-blue-900 dark:bg-blue-950/30 dark:text-gray-300">
      {children}
    </blockquote>
  ),
  code: ({ children }) => (
    <code className="rounded bg-gray-100 px-1.5 py-0.5 font-mono text-[0.875em] text-gray-900 dark:bg-gray-800 dark:text-gray-100">
      {children}
    </code>
  ),
  hr: () => <hr className="mt-10 border-gray-200 dark:border-gray-800" />,
  table: ({ children }) => (
    <div className="mt-6 overflow-x-auto">
      <table className="w-full text-left text-sm">{children}</table>
    </div>
  ),
  th: ({ children }) => (
    <th className="border-b border-gray-300 py-2 pr-4 font-semibold text-gray-900 dark:border-gray-700 dark:text-gray-100">
      {children}
    </th>
  ),
  td: ({ children }) => (
    <td className="border-b border-gray-200 py-2 pr-4 align-top text-gray-700 dark:border-gray-800 dark:text-gray-300">
      {children}
    </td>
  ),
  /**
   * Internal links go through next-intl's `Link` so they keep the reader's locale; anything with a
   * scheme is external and gets the usual new-tab + `noopener` treatment. An article is our own
   * copy, but `noreferrer` costs nothing and keeps our URLs out of third-party referer logs.
   *
   * A path whose last segment has a file extension is a file in `public/` (the tracker
   * spreadsheets), not a route: sending it through `Link` would prefix it with the locale and
   * produce a 404, so it stays a plain download anchor.
   */
  a: ({ href, children }) => {
    const target = href ?? "";
    const linkClassName = "font-medium text-blue-600 underline underline-offset-2 hover:text-blue-700 dark:text-blue-400";

    if (/\.[a-z0-9]{2,5}$/i.test(target) && target.startsWith("/")) {
      return (
        <a href={target} download className={linkClassName}>
          {children}
        </a>
      );
    }

    if (/^https?:\/\//.test(target)) {
      return (
        <a
          href={target}
          target="_blank"
          rel="noopener noreferrer"
          className={linkClassName}
        >
          {children}
        </a>
      );
    }
    return (
      <Link href={target} className={linkClassName}>
        {children}
      </Link>
    );
  },
};

export function useMDXComponents(): MDXComponents {
  return components;
}
