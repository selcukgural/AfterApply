/**
 * Finds the message keys the source actually asks for.
 *
 * The catalogue-parity test compares tr.json against en.json, which catches a key translated into
 * one language only. It cannot catch the opposite mistake: a `t("…")` call whose key was removed
 * from *both* catalogues. That shipped — `landing.roadmap.todayMatch` outlived the feature it
 * described by two months and rendered its own key path as a bullet on the landing page, because
 * next-intl falls back to printing the key rather than failing.
 *
 * This is deliberately a text scan rather than a parse: it only has to understand the one calling
 * convention this codebase uses (`const t = useTranslations("ns")`, then `t("some.key")`).
 */

/** A `t("literal")` call site, resolved against the namespace its variable was bound to. */
export type MessageUsage = { file: string; key: string };

const BINDING = /(?:const|let)\s+(\w+)\s*=\s*(?:await\s+)?(?:get|use)Translations\(\s*"([^"]*)"\s*\)/g;

/**
 * `t("a.b")` and `t.rich("a.b")`, but never `t.has("a.b")` — `.has` is the deliberate probe for a
 * key that may legitimately be absent, and every optional step body in the help centre uses it.
 * Only string literals: a template literal is a dynamic key this scan cannot resolve.
 */
function callsFor(variable: string): RegExp {
  return new RegExp(`(?<![A-Za-z0-9_.$])${variable}(\\.rich)?\\(\\s*"([^"\`$]+)"`, "g");
}

/**
 * Comments are stripped first, so a commented-out call — or an example inside a doc comment, like
 * the ones above — is not mistaken for a live one. Line comments are only recognised at the start
 * of a line: `//` mid-line is far more often the middle of a URL in a string than a comment.
 */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^[ \t]*\/\/.*$/gm, "");
}

export function findMessageUsages(file: string, rawSource: string): MessageUsage[] {
  const source = stripComments(rawSource);
  const namespaces = new Map<string, string[]>();
  for (const match of source.matchAll(BINDING)) {
    const [, variable, namespace] = match;
    namespaces.set(variable, [...(namespaces.get(variable) ?? []), namespace]);
  }

  const usages: MessageUsage[] = [];
  for (const [variable, boundNamespaces] of namespaces) {
    for (const call of source.matchAll(callsFor(variable))) {
      const key = call[2];
      // One variable name can be bound to different namespaces in different scopes of the same
      // file (the OAuth callback pages do this). Resolving under any of them is enough.
      const candidates = boundNamespaces.map((ns) => (ns ? `${ns}.${key}` : key));
      usages.push({ file, key: candidates.join(" | ") });
    }
  }
  return usages;
}

/** True when at least one of the `a | b` alternatives in a resolved usage exists. */
export function isResolvable(usage: MessageUsage, known: Set<string>): boolean {
  return usage.key.split(" | ").some((candidate) => known.has(candidate));
}
