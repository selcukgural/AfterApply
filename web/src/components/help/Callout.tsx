type Variant = "info" | "warning" | "danger";

const VARIANT_CLASSES: Record<Variant, string> = {
  info: "border-blue-200 bg-blue-50 text-blue-900 dark:border-blue-900/60 dark:bg-blue-950/40 dark:text-blue-100",
  warning:
    "border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-100",
  danger: "border-red-200 bg-red-50 text-red-900 dark:border-red-900/60 dark:bg-red-950/40 dark:text-red-100",
};

const ICONS: Record<Variant, React.ReactNode> = {
  info: <path strokeLinecap="round" strokeLinejoin="round" d="M11.25 11.25l.041-.02a.75.75 0 011.063.852l-.708 2.836a.75.75 0 001.063.853l.041-.021M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9-3.75h.008v.008H12V8.25z" />,
  warning: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-8.25 3h.008v.008h-.008V15z"
    />
  ),
  danger: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"
    />
  ),
};

export function Callout({
  variant = "info",
  label,
  title,
  children,
}: {
  variant?: Variant;
  /** Localized label shown above the title, e.g. help.common.note / help.common.warning. */
  label: string;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className={`flex gap-3 rounded-lg border p-4 text-sm ${VARIANT_CLASSES[variant]}`} role="note">
      <svg viewBox="0 0 24 24" className="mt-0.5 h-5 w-5 shrink-0" fill="none" stroke="currentColor" strokeWidth={1.75} aria-hidden="true">
        {ICONS[variant]}
      </svg>
      <div className="flex flex-col gap-1">
        <span className="text-xs font-semibold uppercase tracking-wide opacity-80">{label}</span>
        <p className="font-medium">{title}</p>
        <div className="leading-6 opacity-90">{children}</div>
      </div>
    </div>
  );
}
