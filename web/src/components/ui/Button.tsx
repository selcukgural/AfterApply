import { type ButtonHTMLAttributes } from "react";

type Variant = "primary" | "secondary" | "danger";

const VARIANT_CLASSES: Record<Variant, string> = {
  // The accent token, not Tailwind's stock blue: it is stepped from the logo mark's own gradient
  // and swaps for dark mode on its own (see globals.css).
  primary: "bg-accent text-white hover:bg-accent-strong disabled:bg-accent/40",
  secondary:
    "bg-gray-100 text-gray-900 hover:bg-gray-200 disabled:text-gray-400 dark:bg-gray-800 dark:text-gray-100 dark:hover:bg-gray-700 dark:disabled:text-gray-500",
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
