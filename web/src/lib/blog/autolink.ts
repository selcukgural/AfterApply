/**
 * Which bare text the blog editor may turn into a link on its own — while typing and on paste.
 *
 * TipTap's default links anything with a dot that looks like a host, so "Kariyer.net" in a
 * sentence became an outbound link to kariyer.net, and on paste it overwrote the link the author
 * had actually put on those words (2026-09-22). Our posts name job sites by their domain-shaped
 * brand all the time, so only text that is unmistakably an address is linked: it starts with
 * http(s):// or www. Anything else the author links by hand, with the toolbar or a pasted <a>.
 */
export function shouldAutoLinkBlogText(url: string): boolean {
  return /^https?:\/\/\S/i.test(url) || /^www\.[^\s.]+\.\S/i.test(url);
}
