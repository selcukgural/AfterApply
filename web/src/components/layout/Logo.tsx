// "ek" mark with an upward arrow (2026 rebrand). Raster asset with its own blue-to-green
// gradient, so unlike the old currentColor glyph it doesn't tint with text color — the
// gradient reads fine on both light and dark surfaces on its own.
export function LogoMark({ className = "" }: { className?: string }) {
  // eslint-disable-next-line @next/next/no-img-element
  return <img src="/brand/logo-mark.png" alt="" className={className} aria-hidden="true" />;
}

export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      <LogoMark className="h-6 w-6 shrink-0" />
      <span className="text-lg font-semibold text-gray-900 dark:text-gray-100">e-kariyerim</span>
    </span>
  );
}
