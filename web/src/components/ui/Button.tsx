import { type ButtonHTMLAttributes } from "react";

type Variant = "primary" | "secondary" | "outline" | "danger";

const VARIANT_CLASSES: Record<Variant, string> = {
  // The accent token, not Tailwind's stock blue: it is stepped from the logo mark's own gradient
  // and swaps for dark mode on its own (see globals.css).
  primary: "bg-accent text-white hover:bg-accent-strong disabled:bg-accent/40",
  secondary:
    "bg-gray-100 text-gray-900 hover:bg-gray-200 disabled:text-gray-400 dark:bg-gray-800 dark:text-gray-100 dark:hover:bg-gray-700 dark:disabled:text-gray-500",
  // A ring rather than a border, and that is the whole reason this variant exists as a token: a
  // real border adds a pixel on each side, so an outline button standing next to a primary one in
  // the navbar would be 2px taller than it. A ring adds no layout box.
  outline:
    "text-accent-ink ring-1 ring-inset ring-accent/50 hover:bg-accent/10 disabled:text-gray-400 disabled:ring-gray-300 dark:hover:bg-accent/20",
  danger: "bg-red-600 text-white hover:bg-red-700 disabled:bg-red-300",
};

// Exported so non-<button> elements that need to look like a Button (e.g. a
// next-intl <Link> used as a CTA) can share the same visual style instead of
// nesting an actual <button> inside an <a>, which is invalid HTML.
export function buttonClassName(variant: Variant = "primary", className = ""): string {
  return `rounded-md px-4 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed ${VARIANT_CLASSES[variant]} ${className}`;
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  return <button className={buttonClassName(variant, className)} {...props} />;
}
