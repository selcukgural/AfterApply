"use client";

import { useEffect, useRef } from "react";

/** `indeterminate` is a DOM property with no HTML attribute, so it has to be assigned to the node —
 *  React will not set it from JSX. */
export function SelectionCheckbox({
  checked,
  indeterminate,
  onChange,
  label,
}: {
  checked: boolean;
  indeterminate: boolean;
  onChange: () => void;
  label: string;
}) {
  const ref = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (ref.current) {
      ref.current.indeterminate = indeterminate;
    }
  }, [indeterminate]);

  return (
    <input
      ref={ref}
      type="checkbox"
      checked={checked}
      onChange={onChange}
      aria-label={label}
      className="h-4 w-4 rounded border-gray-300 text-accent focus:ring-1 focus:ring-blue-500 dark:border-gray-700 dark:bg-gray-900"
    />
  );
}
