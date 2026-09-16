/**
 * React Query keys for the weekly-jobs feature. A module of its own (no messages, no
 * components) so the navbar can share the status query without pulling the criteria form —
 * and its whole message namespace — into every layout's import graph.
 */
export const WEEKLY_JOBS_QUERY_KEYS = {
  status: ["weeklyJobs", "status"] as const,
  profile: ["weeklyJobs", "profile"] as const,
  postings: (week?: number) => ["weeklyJobs", "postings", week ?? "latest"] as const,
  posting: (id: string) => ["weeklyJobs", "posting", id] as const,
};
