// Chat-bubble + checkmark mark — "you applied, you got a real answer" (Sprint 15 rebrand,
// concept picked by the user from the design canvas). currentColor so it inherits the
// wrapping element's text color and themes automatically with the rest of the app.
export function LogoMark({ className = "" }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <path d="M4 5.5h16a1 1 0 011 1v9a1 1 0 01-1 1H9.5l-4 3.5v-3.5H4a1 1 0 01-1-1v-9a1 1 0 011-1z" />
      <path d="M8 10.5l2.4 2.4L16.5 7.5" />
    </svg>
  );
}

export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      <LogoMark className="h-6 w-6 shrink-0 text-blue-600 dark:text-blue-400" />
      <span className="text-lg font-semibold text-gray-900 dark:text-gray-100">e-kariyerim</span>
    </span>
  );
}
