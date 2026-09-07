"use client";

import { useEffect, useRef } from "react";

interface ModalProps {
  /** Rendered as the dialog's accessible name. */
  title: string;
  /** Esc, the backdrop, and the close button all route here. Never called while `busy`. */
  onClose: () => void;
  /** Blocks the dismissal paths while a request is in flight, so a half-applied bulk operation
   *  cannot lose the screen that explains what it did. */
  busy?: boolean;
  children: React.ReactNode;
  footer: React.ReactNode;
}

/**
 * A modal built on the native `<dialog>` element rather than a hand-rolled overlay: focus trapping,
 * inertness of the page behind, Esc-to-close and the top layer all come from the platform, which is
 * a lot of accessibility this app would otherwise have to get right by hand.
 *
 * This is the first real dialog in the app — the destructive flows that predate it use
 * `window.confirm`, which cannot show a row list, a checkbox, or a typed confirmation.
 */
export function Modal({ title, onClose, busy = false, children, footer }: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  // Opens once, on mount, and closes on unmount. Deliberately depends on nothing else: re-running
  // it on a prop change would call showModal() on an already-open dialog.
  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog || dialog.open) {
      return;
    }
    dialog.showModal();
    return () => dialog.close();
  }, []);

  return (
    <dialog
      ref={dialogRef}
      aria-label={title}
      onCancel={(event) => {
        // The browser closes on Esc by default; take that back while a request is running so the
        // result cannot vanish mid-flight.
        event.preventDefault();
        if (!busy) {
          onClose();
        }
      }}
      onClick={(event) => {
        // A click that lands on the <dialog> itself is a click on its backdrop — the content sits
        // in the child <div>, which stops the event from ever reaching here.
        if (event.target === dialogRef.current && !busy) {
          onClose();
        }
      }}
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-xl border border-gray-200 bg-white p-0 text-gray-900 shadow-2xl backdrop:bg-gray-900/50 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-100"
    >
      <div className="flex max-h-[calc(100dvh-6rem)] flex-col">
        <div className="flex-1 overflow-y-auto px-6 pt-6">{children}</div>
        <div className="flex items-center justify-end gap-2 px-6 pb-6 pt-5">{footer}</div>
      </div>
    </dialog>
  );
}
