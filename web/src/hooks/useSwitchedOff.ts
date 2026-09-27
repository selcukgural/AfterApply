"use client";

import { useClientConfig } from "@/hooks/useClientConfig";
import { switchedOff, type SwitchedOff } from "@/lib/config/switchedOff";

/** `switchedOff` over the live config — and over nothing until the config has answered, so the
 *  built-in default is never read as an admin's switch. */
export function useSwitchedOff(): SwitchedOff {
  const { config, isLoaded } = useClientConfig();
  return switchedOff(isLoaded ? config : null);
}
