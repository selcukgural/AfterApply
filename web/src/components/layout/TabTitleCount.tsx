"use client";

import { useEffect } from "react";
import { useReminders, useUpcomingInterviews } from "@/hooks/useReminders";
import { pendingCount, titleWithCount } from "@/lib/layout/tabTitle";

/**
 * Keeps "(2) " in front of the tab title while reminders are due or an interview is today, so the
 * tab says so from across the tab strip. Reads the same queries the dashboard card does (page 1
 * shares its cache), and renders nothing.
 */
export function TabTitleCount() {
  const { data: reminders } = useReminders(1);
  const { data: interviews } = useUpcomingInterviews();
  const count = pendingCount(reminders?.totalCount ?? 0, interviews ?? []);

  useEffect(() => {
    const apply = () => {
      const next = titleWithCount(document.title, count);
      if (next !== document.title) document.title = next;
    };
    apply();
    // Each navigation writes a fresh page title; put the count back in front of it.
    const observer = new MutationObserver(apply);
    observer.observe(document.head, { subtree: true, childList: true, characterData: true });
    return () => {
      observer.disconnect();
      document.title = titleWithCount(document.title, 0);
    };
  }, [count]);

  return null;
}
