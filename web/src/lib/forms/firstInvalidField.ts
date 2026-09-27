/**
 * Which of a form's fields, in on-screen order, is the first one with an error. A field matches
 * its error key either exactly (`jobTitle`) or as a prefixed id (`application-hrEmail`), the way
 * shared field groups namespace their inputs.
 */
export function firstInvalidFieldId(idsInOrder: readonly string[], errorKeys: readonly string[]): string | null {
  const keys = errorKeys.filter(Boolean);
  return idsInOrder.find((id) => keys.some((key) => id === key || id.endsWith(`-${key}`))) ?? null;
}

/**
 * After a failed submit, take the user to the first thing to fix instead of leaving them to hunt
 * for a red line somewhere below the fold.
 */
export function focusFirstInvalidField(form: HTMLFormElement, errorKeys: readonly string[]): void {
  const fields = Array.from(form.querySelectorAll<HTMLElement>("input[id], select[id], textarea[id]"));
  const id = firstInvalidFieldId(
    fields.map((field) => field.id),
    errorKeys,
  );
  const target = fields.find((field) => field.id === id);
  if (!target) return;
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  target.scrollIntoView({ block: "center", behavior: reduceMotion ? "auto" : "smooth" });
  target.focus({ preventScroll: true });
}
