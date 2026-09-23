import type { NotificationPreferences } from "@/types/api";

export type ContributionPreferenceKey = "reviewHelpful" | "salaryHelpful" | "experienceHelpful" | "blogCommentHelpful";

/** The four kinds under the master switch, in the order the settings card lists them. */
export const CONTRIBUTION_PREFERENCE_KEYS: readonly ContributionPreferenceKey[] = [
  "reviewHelpful",
  "salaryHelpful",
  "experienceHelpful",
  "blogCommentHelpful",
];

/** What a kind's switch shows: its own value, but off and locked while the master is off — the
 *  saved value is kept underneath and comes back when the master does. */
export function effectiveSwitch(preferences: NotificationPreferences, key: ContributionPreferenceKey): { on: boolean; locked: boolean } {
  return preferences.contributions ? { on: preferences[key], locked: false } : { on: false, locked: true };
}

export function toggled(preferences: NotificationPreferences, key: keyof NotificationPreferences): NotificationPreferences {
  return { ...preferences, [key]: !preferences[key] };
}
