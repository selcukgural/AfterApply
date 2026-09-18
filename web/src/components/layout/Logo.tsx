// "ek" mark with an upward arrow (2026 rebrand). Raster asset with its own blue-to-green
// gradient, so unlike the old currentColor glyph it doesn't tint with text color — the
// gradient reads fine on both light and dark surfaces on its own. Served as a 128px WebP (6 KB;
// the mark never renders above 24 px) — the 256px PNG (33 KB) stays for the JSON-LD `logo`,
// which Google wants as PNG/JPG.
export function LogoMark({ className = "" }: { className?: string }) {
  // eslint-disable-next-line @next/next/no-img-element
  return <img src="/brand/logo-mark.webp" alt="" width={128} height={128} className={className} aria-hidden="true" />;
}

export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      <LogoMark className="h-6 w-6 shrink-0" />
      <span className="text-lg font-semibold whitespace-nowrap text-gray-900 dark:text-gray-100">e-kariyerim</span>
    </span>
  );
}
