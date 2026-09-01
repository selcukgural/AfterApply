export interface Step {
  title: string;
  body?: string;
  visual?: React.ReactNode;
}

export function StepList({ steps }: { steps: Step[] }) {
  return (
    <ol className="flex flex-col gap-6">
      {steps.map((step, index) => (
        <li key={step.title} className="flex gap-4">
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-blue-600 text-sm font-semibold text-white">
            {index + 1}
          </span>
          <div className="flex flex-col gap-2 pt-0.5">
            <p className="font-medium text-gray-900 dark:text-gray-100">{step.title}</p>
            {step.body && <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{step.body}</p>}
            {step.visual}
          </div>
        </li>
      ))}
    </ol>
  );
}
