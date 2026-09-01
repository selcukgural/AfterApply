export function GifFigure({ src, alt, caption }: { src: string; alt: string; caption?: string }) {
  return (
    <figure className="flex flex-col gap-2">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt={alt} className="w-full rounded-lg border border-gray-200 dark:border-gray-800" />
      {caption && <figcaption className="text-xs text-gray-500 dark:text-gray-400">{caption}</figcaption>}
    </figure>
  );
}
