/**
 * The handoff between the landing page's drop zone and the scan page.
 *
 * A visitor drops a CV in the hero, but the consent box, the result screen and the rate limit all
 * live on `/cv-tarama` — and a `File` cannot travel through a URL. So it travels through this
 * module: the hero stashes it, navigates, and the form takes it as its pre-selected file. Both
 * pages are the same client bundle across a soft navigation, so the module instance is shared.
 *
 * **Memory only, deliberately.** Nothing here touches sessionStorage, localStorage or IndexedDB.
 * `web/src/lib/privacy/browserStorage.test.ts` pins every storage key this app writes and
 * `/cookies` publishes that inventory as fact; putting a stranger's CV on their disk would make
 * the published page false. The consequence — a hard reload loses the file — is the correct
 * behaviour rather than a defect: the form then renders as an ordinary empty drop zone, which is
 * exactly what someone who reloaded expects.
 *
 * **Dropping a file is not consent.** All this moves is bytes. The consent box on the scan page
 * still arrives unticked, and the visitor still presses the button; the server refuses anything
 * else.
 */

export interface PendingScanFile {
  file: File;
  /** When the visitor chose the file, not when the form mounted. The scan page seeds its
   *  form-timing guard from this — see CvScanForm. */
  pickedAt: number;
}

/** A file picked up but never collected should not outlive the visitor's attention. A `File`
 *  handle keeps the underlying blob alive, so an interrupted navigation would otherwise park a
 *  CV in memory for the rest of the session. */
const MAX_AGE_MS = 5 * 60_000;

let pending: PendingScanFile | null = null;

export function stashScanFile(file: File): void {
  pending = { file, pickedAt: Date.now() };
}

/**
 * Takes the stashed file, if there is a fresh one. **One shot:** the slot is cleared as it is
 * read, so navigating back and then forward again does not resurrect a file the visitor has
 * already dealt with.
 */
export function takeScanFile(): PendingScanFile | null {
  const taken = pending;
  pending = null;

  if (taken === null || Date.now() - taken.pickedAt > MAX_AGE_MS) {
    return null;
  }

  return taken;
}
