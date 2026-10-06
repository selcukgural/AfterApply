// "ek" mark with an upward arrow (2026 rebrand). Raster asset with its own blue-to-green
// gradient, so unlike the old currentColor glyph it doesn't tint with text color — the
// gradient reads fine on both light and dark surfaces on its own. The mark never renders above
// 24 px, so it is served at 48 px (1.6 KB; enough up to a 2x screen) with a 72 px copy for 3x
// screens — the 128 px file it replaced was 6 KB and PageSpeed flagged it (2026-10-06). The
// 256px PNG (33 KB) stays for the JSON-LD `logo`, which Google wants as PNG/JPG.
export function LogoMark({ className = "" }: { className?: string }) {
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src="/brand/logo-mark-48.webp"
      srcSet="/brand/logo-mark-48.webp 48w, /brand/logo-mark-72.webp 72w"
      sizes="24px"
      alt=""
      width={48}
      height={48}
      className={className}
      aria-hidden="true"
    />
  );
}

export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      <LogoMark className="h-6 w-6 shrink-0" />
      <span className="text-lg font-semibold whitespace-nowrap text-gray-900 dark:text-gray-100">e-kariyerim</span>
    </span>
  );
}
