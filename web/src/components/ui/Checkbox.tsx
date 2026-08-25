import { type InputHTMLAttributes } from "react";

interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
  label: React.ReactNode;
  error?: string;
}

export function Checkbox({ id, label, error, className = "", ...props }: CheckboxProps) {
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="flex items-start gap-2 text-sm text-gray-700 dark:text-gray-300">
        <input
          id={id}
          type="checkbox"
          className={`mt-0.5 h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-1 focus:ring-blue-500 dark:border-gray-700 dark:bg-gray-900 ${className}`}
          {...props}
        />
        <span>{label}</span>
      </label>
      {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}
